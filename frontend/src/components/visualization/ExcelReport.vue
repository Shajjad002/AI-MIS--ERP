<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { WorkBook } from 'xlsx'
import { apiFetch } from '../../config/api'

type CellValue = string | number | boolean | Date | null
type ReportRow = Record<string, CellValue>
type ChartMode = 'bar' | 'donut'
type SavedImport = { id: string; fileName: string; worksheetName: string; rowCount: number; importedAt: string }
type SavedReport = SavedImport & { columns: string[]; rows: unknown[][] }

const maxFileBytes = 20 * 1024 * 1024
const maxRequestBytes = 18 * 1024 * 1024
const maxReportRows = 20_000
const workbook = ref<WorkBook | null>(null)
let spreadsheetModule: typeof import('xlsx') | null = null
const sheetNames = ref<string[]>([])
const selectedSheet = ref('')
const headerRowNumber = ref(1)
const fileName = ref('')
const columns = ref<string[]>([])
const rows = ref<ReportRow[]>([])
const groupColumn = ref('')
const metricColumn = ref('')
const chartMode = ref<ChartMode>('bar')
const error = ref('')
const success = ref('')
const importId = ref('')
const loading = ref(false)
const headerRowLimit = computed(() => {
  const sheet = workbook.value?.Sheets[selectedSheet.value]
  if (!sheet || !spreadsheetModule) return 1
  return spreadsheetModule.utils.decode_range(sheet['!ref'] || 'A1').e.r + 1
})

function detectHeaderRow(matrix: unknown[][]) {
  const scanRows = matrix.slice(0, 30)
  let bestIndex = 0
  let bestCount = 0
  scanRows.forEach((row, index) => {
    const count = row.filter(value => value !== null && value !== undefined && String(value).trim() !== '').length
    if (count > bestCount) {
      bestCount = count
      bestIndex = index
    }
  })
  return bestIndex + 1
}

function asNumber(value: CellValue): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) return value
  if (typeof value !== 'string' || !value.trim()) return null
  const normalized = value.trim().replace(/,/g, '')
  const parsed = Number(normalized)
  return Number.isFinite(parsed) ? parsed : null
}

const numericColumns = computed(() => columns.value.filter(column => {
  const values = rows.value.map(row => row[column]).filter(value => value !== null && value !== '')
  return values.length > 0 && values.every(value => asNumber(value) !== null)
}))
const reportColumns = computed(() => columns.value.filter(column => !numericColumns.value.includes(column)))

watch([reportColumns, numericColumns], ([categories, metrics]) => {
  if (!categories.includes(groupColumn.value)) groupColumn.value = categories[0] ?? columns.value[0] ?? ''
  if (!metrics.includes(metricColumn.value)) metricColumn.value = metrics[0] ?? ''
}, { immediate: true })

const groupedRows = computed(() => {
  if (!groupColumn.value || !metricColumn.value) return []
  const groups = new Map<string, number>()
  for (const row of rows.value) {
    if (Object.values(row).some(value => typeof value === 'string' && /^total\b/i.test(value.trim()))) continue
    const rawLabel = row[groupColumn.value]
    const label = rawLabel instanceof Date
      ? rawLabel.toLocaleDateString()
      : rawLabel === null || rawLabel === '' ? '(Blank)' : String(rawLabel)
    const amount = asNumber(row[metricColumn.value])
    if (amount !== null) groups.set(label, (groups.get(label) ?? 0) + amount)
    else if (!groups.has(label)) groups.set(label, 0)
  }
  return [...groups.entries()]
    .map(([label, value]) => ({ label, value }))
    .sort((left, right) => right.value - left.value)
})
const chartRows = computed(() => groupedRows.value.slice(0, 12))
const total = computed(() => rows.value.reduce((sum, row) => sum + (asNumber(row[metricColumn.value]) ?? 0), 0))
const maximumMagnitude = computed(() => Math.max(...chartRows.value.map(row => Math.abs(row.value)), 0))
const donutTotal = computed(() => chartRows.value.reduce((sum, row) => sum + Math.max(0, row.value), 0))
const donutGradient = computed(() => {
  if (!donutTotal.value) return 'var(--ui-border) 0 100%'
  const colors = ['#0ea5e9', '#6366f1', '#8b5cf6', '#14b8a6', '#f59e0b', '#ef4444', '#84cc16', '#ec4899']
  let start = 0
  return chartRows.value.map((row, index) => {
    const end = start + Math.max(0, row.value) / donutTotal.value * 100
    const segment = `${colors[index % colors.length]} ${start}% ${end}%`
    start = end
    return segment
  }).join(', ')
})
const previewRows = computed(() => rows.value.slice(0, 100))

function formatNumber(value: number) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value)
}

function displayCell(value: CellValue) {
  if (value === null || value === '') return '—'
  if (value instanceof Date) return value.toLocaleDateString()
  return String(value)
}

async function readSelectedSheet() {
  const sheet = workbook.value?.Sheets[selectedSheet.value]
  if (!sheet) return
  error.value = ''
  success.value = ''
  importId.value = ''
  columns.value = []
  rows.value = []
  loading.value = true
  try {
    const matrix = spreadsheetModule?.utils.sheet_to_json<unknown[]>(sheet, { header: 1, defval: null, raw: true, blankrows: true })
    if (!matrix) throw new Error('The worksheet could not be read.')
    const headerIndex = headerRowNumber.value - 1
    const headerCells = matrix[headerIndex]
    if (!headerCells || !matrix.slice(headerIndex + 1).some(row => row.some(value => value !== null && value !== ''))) {
      throw new Error('Choose a header row followed by at least one row of data.')
    }

    const dataRowsSource = matrix.slice(headerIndex + 1).filter(row => row.some(value => value !== null && value !== ''))
    const width = Math.max(headerCells.length, ...dataRowsSource.map(row => row.length))
    const seen = new Map<string, number>()
    const importColumns = Array.from({ length: width }, (_, index) => {
      const base = String(headerCells[index] ?? '').trim() || `Column ${index + 1}`
      const key = base.toLocaleLowerCase()
      const count = (seen.get(key) ?? 0) + 1
      seen.set(key, count)
      return count === 1 ? base : `${base} (${count})`
    })
    if (importColumns.length > 100) throw new Error('A worksheet can contain up to 100 columns.')

    const availableRows = dataRowsSource
    const wasTruncated = availableRows.length > maxReportRows
    const dataRows = availableRows.slice(0, maxReportRows)
      .map(row => importColumns.map((_, index) => {
        const value = row[index]
        return value instanceof Date || typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean' ? value : null
      }))
    if (!dataRows.length) throw new Error('This worksheet does not contain any data rows.')

    const token = localStorage.getItem('ai-mis.access-token')
    if (!token) throw new Error('Sign in before importing a workbook.')
    const requestBody = JSON.stringify({
      fileName: fileName.value,
      worksheetName: selectedSheet.value,
      columns: importColumns,
      rows: dataRows,
    })
    if (new TextEncoder().encode(requestBody).length > maxRequestBytes) {
      throw new Error('This worksheet is too large to save. Reduce it to fewer rows or columns and try again.')
    }

    const headers = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }
    const saveResponse = await apiFetch('/api/excelImports', { method: 'POST', headers, body: requestBody })
    const saveBody = await saveResponse.json() as SavedImport & { message?: string }
    if (!saveResponse.ok) throw new Error(saveBody.message || `The workbook could not be saved (HTTP ${saveResponse.status}).`)

    importId.value = saveBody.id
    const reportResponse = await apiFetch(`/api/excelImports/${encodeURIComponent(saveBody.id)}`, { headers })
    const reportBody = await reportResponse.json() as SavedReport & { message?: string }
    if (!reportResponse.ok) throw new Error(reportBody.message || `The saved report could not be loaded (HTTP ${reportResponse.status}).`)

    columns.value = reportBody.columns
    rows.value = reportBody.rows.map(values => Object.fromEntries(reportBody.columns.map((column, index) => [column, values[index] ?? null])) as ReportRow)
    success.value = `Saved ${reportBody.rowCount.toLocaleString()} rows in AiMisErpCopilotDb${wasTruncated ? ` (first ${maxReportRows.toLocaleString()} of ${availableRows.length.toLocaleString()} rows)` : ''}. The report below uses the saved data.`
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : 'The workbook could not be imported.'
  } finally {
    loading.value = false
  }
}

function selectSheet() {
  const sheet = workbook.value?.Sheets[selectedSheet.value]
  if (!sheet || !spreadsheetModule) return
  const matrix = spreadsheetModule.utils.sheet_to_json<unknown[]>(sheet, { header: 1, defval: null, raw: true, blankrows: true })
  headerRowNumber.value = detectHeaderRow(matrix)
  void readSelectedSheet()
}

async function importFile(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  error.value = ''
  if (file.size > maxFileBytes) {
    error.value = 'Choose a workbook smaller than 20 MB.'
    input.value = ''
    return
  }

  loading.value = true
  try {
    spreadsheetModule = await import('xlsx')
    const parsed = spreadsheetModule.read(await file.arrayBuffer(), { type: 'array', cellDates: true })
    if (!parsed.SheetNames.length) throw new Error('No worksheets were found in this file.')
    workbook.value = parsed
    sheetNames.value = parsed.SheetNames
    selectedSheet.value = parsed.SheetNames[0]
    const matrix = spreadsheetModule.utils.sheet_to_json<unknown[]>(parsed.Sheets[selectedSheet.value], { header: 1, defval: null, raw: true, blankrows: true })
    headerRowNumber.value = detectHeaderRow(matrix)
    fileName.value = file.name
    await readSelectedSheet()
  } catch (cause) {
    workbook.value = null
    sheetNames.value = []
    columns.value = []
    rows.value = []
    error.value = cause instanceof Error ? `Could not read this workbook: ${cause.message}` : 'Could not read this workbook.'
  } finally {
    loading.value = false
    input.value = ''
  }
}
</script>

<template>
  <section class="excel-report" aria-labelledby="excel-report-title">
    <div class="excel-intro">
      <div>
        <p class="eyebrow">Spreadsheet analysis</p>
        <h2 id="excel-report-title">Build a report from Excel</h2>
        <p class="hint">Workbook data is saved to AiMisErpCopilotDb before the graphical report is shown.</p>
      </div>
      <label class="excel-upload-button" for="excel-report-file">{{ loading ? 'Saving to database…' : 'Choose Excel file' }}</label>
      <input id="excel-report-file" class="excel-file-input" type="file" accept=".xlsx,.xls,.xlsm,.csv" :disabled="loading" @change="importFile" />
    </div>

    <p v-if="error" class="excel-message" :class="{ 'excel-warning': rows.length }" role="status">{{ error }}</p>
    <p v-if="success" class="excel-success" role="status">{{ success }} <span v-if="importId">Import ID: {{ importId }}</span></p>
    <div v-if="fileName" class="excel-workbook-bar">
      <div><strong>{{ fileName }}</strong><span>{{ rows.length.toLocaleString() }} rows · {{ columns.length }} columns</span></div>
      <label v-if="sheetNames.length > 1" class="excel-sheet-select">Worksheet
        <select v-model="selectedSheet" :disabled="loading" @change="selectSheet">
          <option v-for="sheet in sheetNames" :key="sheet" :value="sheet">{{ sheet }}</option>
        </select>
      </label>
      <label v-if="workbook" class="excel-sheet-select">Header row
        <input v-model.number="headerRowNumber" type="number" min="1" :max="headerRowLimit" :disabled="loading" @change="void readSelectedSheet()" />
      </label>
    </div>

    <div v-if="rows.length" class="excel-report-content">
      <div class="excel-controls">
        <label>Group rows by
          <select v-model="groupColumn"><option v-for="column in columns" :key="column" :value="column">{{ column }}</option></select>
        </label>
        <label>Measure
          <select v-model="metricColumn" :disabled="numericColumns.length === 0">
            <option v-for="column in numericColumns" :key="column" :value="column">{{ column }}</option>
          </select>
        </label>
        <label>Chart
          <select v-model="chartMode"><option value="bar">Bar chart</option><option value="donut">Donut chart</option></select>
        </label>
      </div>

      <div class="excel-summary-grid">
        <article class="excel-summary-card"><span>Data rows</span><strong>{{ rows.length.toLocaleString() }}</strong></article>
        <article class="excel-summary-card"><span>Total {{ metricColumn || 'value' }}</span><strong>{{ numericColumns.length ? formatNumber(total) : 'No numeric column' }}</strong></article>
        <article class="excel-summary-card"><span>Groups</span><strong>{{ groupedRows.length.toLocaleString() }}</strong></article>
      </div>

      <section class="excel-chart-card" aria-label="Excel report chart">
        <div class="card-heading"><div><h3>{{ metricColumn ? `${metricColumn} by ${groupColumn}` : 'Choose a numeric measure' }}</h3><span>Top {{ chartRows.length }} groups by total</span></div></div>
        <p v-if="!numericColumns.length" class="notice">No numeric columns were detected. Add a numeric measure to your worksheet to create a chart.</p>
        <div v-else-if="chartMode === 'bar'" class="excel-bar-chart">
          <div v-for="row in chartRows" :key="row.label" class="excel-bar-row">
            <span class="excel-bar-label" :title="row.label">{{ row.label }}</span>
            <span class="excel-bar-track"><span class="excel-bar-fill" :style="{ width: `${maximumMagnitude ? Math.abs(row.value) / maximumMagnitude * 100 : 0}%` }" /></span>
            <strong>{{ formatNumber(row.value) }}</strong>
          </div>
        </div>
        <div v-else class="excel-donut-layout">
          <div class="excel-donut" :style="{ background: `conic-gradient(${donutGradient})` }"><strong>{{ formatNumber(donutTotal) }}</strong></div>
          <ul class="excel-legend">
            <li v-for="(row, index) in chartRows" :key="row.label"><span :class="`excel-legend-color color-${index % 8}`" />{{ row.label }}<strong>{{ formatNumber(row.value) }}</strong></li>
          </ul>
        </div>
        <p v-if="chartMode === 'donut' && chartRows.some(row => row.value < 0)" class="hint">Donut charts show positive values only. Use the bar chart to compare negative values.</p>
      </section>

      <section class="excel-table-card">
        <div class="card-heading"><div><h3>Workbook preview</h3><span>First {{ previewRows.length }} of {{ rows.length.toLocaleString() }} rows</span></div></div>
        <div class="table-scroll">
          <table>
            <thead><tr><th v-for="column in columns" :key="column">{{ column }}</th></tr></thead>
            <tbody><tr v-for="(row, index) in previewRows" :key="index"><td v-for="column in columns" :key="column">{{ displayCell(row[column]) }}</td></tr></tbody>
          </table>
        </div>
      </section>
      <p class="excel-footnote">Formula cells use the saved values in the workbook. Recalculate the file in Excel before importing if those values are out of date.</p>
    </div>
    <div v-else-if="!error" class="excel-empty-state">
      <strong>Start with an Excel workbook</strong>
      <p>Use a worksheet with column headers in the first row and numeric values in at least one column.</p>
      <span>Supported formats: .xlsx, .xls, .xlsm, .csv · Up to 20 MB, 20,000 rows, and 100 columns</span>
    </div>
  </section>
</template>
