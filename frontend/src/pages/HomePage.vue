<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { apiFetch } from '../config/api'

const props = defineProps<{ userName: string | null }>()
const emit = defineEmits<{ openVisualization: [] }>()
const chartMode = ref('line')

type PortfolioRow = {
  branch?: string
  branchName?: string
  branchCode?: string
  area?: string
  areaName?: string
  areaCode?: string
  region?: string
  regionName?: string
  regionCode?: string
  zone?: string
  zoneName?: string
  zoneCode?: string
  disbursement: number
}

type PortfolioMetrics = {
  avarageSavingsOutstandingRatio: number
  avaragePARRatio: number
  avarageOTR: number
  avarageOverdueOutstandingRatio: number
  totalDisbursement: number
  totalMember: number
  totalBorrower: number
  totalOverdueBorrower: number
  branchProtfolioList?: PortfolioRow[]
  areaWiseBranchProtfolioList?: PortfolioRow[]
  regionWiseBranchProtfolioList?: PortfolioRow[]
  zoneWiseBranchProtfolioList?: PortfolioRow[]
}

type DashboardResponse = {
  isSuccessful: boolean
  message: string
  data?: {
    branchPortfolio?: PortfolioMetrics
  }
}

const now = new Date()
const currentDate = formatDate(now)
const dateRangeStart = ref(formatDate(new Date(now.getFullYear(), now.getMonth(), 1)))
const dateRangeEnd = ref(currentDate)
const selectedViewBy = ref('Zone')
const viewByOptions = ['Zone', 'Region', 'Area', 'Branch']
const dashboardMetrics = ref<PortfolioMetrics | null>(null)
const dailyDisbursementSeries = ref<Array<{ date: string; label: string; value: number }>>([])
const dashboardLoading = ref(false)
const dailyDisbursementLoading = ref(false)
const dashboardError = ref('')
const dailyDisbursementError = ref('')
let dashboardLoadId = 0
let activeDashboardRequest: AbortController | null = null
const topCards = computed(() => [
  { label: 'Savings / Outstanding Ratio(%)', value: formatMetric(dashboardMetrics.value?.avarageSavingsOutstandingRatio), tone: 'orange' },
  { label: 'PAR(%)', value: formatMetric(dashboardMetrics.value?.avaragePARRatio), tone: 'blue' },
  { label: 'OTR(%)', value: formatMetric(dashboardMetrics.value?.avarageOTR), tone: 'purple' },
  { label: 'Overdue / Outstanding Ratio(%)', value: formatMetric(dashboardMetrics.value?.avarageOverdueOutstandingRatio), tone: 'green' },
  { label: 'Churn Rate', value: '3.21%', tone: 'red' },
])

function formatDate(date: Date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function formatMetric(value: number | undefined) {
  return value === undefined || !Number.isFinite(value) ? '—' : `${value.toFixed(2)}%`
}

async function loadDashboard() {
  const loadId = ++dashboardLoadId
  activeDashboardRequest?.abort()
  const controller = new AbortController()
  activeDashboardRequest = controller
  dashboardLoading.value = true
  dailyDisbursementLoading.value = false
  dashboardError.value = ''
  dailyDisbursementError.value = ''
  dailyDisbursementSeries.value = []

  let dates: string[] = []

  try {
    const token = localStorage.getItem('ai-mis.access-token')
    if (!token) {
      throw new Error('Sign in to load dashboard metrics.')
    }

    const cursor = new Date(`${dateRangeStart.value}T00:00:00`)
    const rangeEnd = new Date(`${dateRangeEnd.value}T00:00:00`)
    if (cursor > rangeEnd) {
      throw new Error('The start date must be on or before the end date.')
    }
    while (cursor <= rangeEnd) {
      if (dates.length === 31) {
        throw new Error('Daily disbursement charts support date ranges of up to 31 days. Select a shorter range.')
      }
      dates.push(formatDate(cursor))
      cursor.setDate(cursor.getDate() + 1)
    }

    const requestPortfolio = async (fromDate: string, toDate: string, summaryOnly = false) => {
      const params = new URLSearchParams({
        fromDate,
        toDate,
        programCode: '001',
        divisionCode: '',
        zoneCode: '',
        regionCode: '',
        areaCode: '',
        viewBy: selectedViewBy.value,
        branchCode: '',
      })
      if (summaryOnly) params.set('summaryOnly', 'true')
      const response = await apiFetch(`/api/misReports?${params}`, {
        headers: { Authorization: `Bearer ${token}` },
        signal: controller.signal,
      })
      const body = await response.json() as DashboardResponse
      if (!response.ok) {
        throw new Error(body.message || `Dashboard request failed (HTTP ${response.status}).`)
      }
      if (!body.isSuccessful && body.message !== 'No Data Found') {
        throw new Error(body.message || 'Dashboard report could not be loaded.')
      }
      return body.isSuccessful ? body.data?.branchPortfolio ?? null : null
    }

    const portfolio = await requestPortfolio(dateRangeStart.value, dateRangeEnd.value)
    if (loadId !== dashboardLoadId) return
    if (!portfolio) {
      throw new Error('No dashboard data was returned for this date range.')
    }
    dashboardMetrics.value = portfolio
    dashboardLoading.value = false
    dailyDisbursementLoading.value = true
    try {
      const dailyValues = new Array<{ date: string; total: number } | undefined>(dates.length)
      const dateLabel = new Intl.DateTimeFormat('en', { month: 'short', day: 'numeric' })
      let nextDateIndex = 0
      const loadNextDate = async () => {
        while (nextDateIndex < dates.length && loadId === dashboardLoadId) {
          const index = nextDateIndex++
          const date = dates[index]
          const portfolio = await requestPortfolio(date, date, true)
          if (loadId !== dashboardLoadId) return
          dailyValues[index] = { date, total: portfolio?.totalDisbursement ?? 0 }
          dailyDisbursementSeries.value = dailyValues.flatMap(item => item
            ? [{
                date: item.date,
                label: dateLabel.format(new Date(`${item.date}T00:00:00`)),
                value: item.total,
              }]
            : [])
        }
      }

      await Promise.all(Array.from(
        { length: Math.min(4, dates.length) },
        () => loadNextDate(),
      ))
      if (loadId !== dashboardLoadId) return
    } catch (error) {
      if (loadId !== dashboardLoadId) return
      dailyDisbursementError.value = error instanceof Error
        ? error.message
        : 'Daily disbursement data could not be loaded.'
    }
  } catch (error) {
    if (loadId !== dashboardLoadId) return
    dashboardMetrics.value = null
    dashboardError.value = error instanceof Error ? error.message : 'Dashboard metrics could not be loaded.'
  } finally {
    if (loadId === dashboardLoadId) {
      dashboardLoading.value = false
      dailyDisbursementLoading.value = false
    }
  }
}

onBeforeUnmount(() => {
  dashboardLoadId += 1
  activeDashboardRequest?.abort()
})

onMounted(() => void loadDashboard())

const disbursementTotal = computed(() => dashboardMetrics.value?.totalDisbursement ?? 0)
const formattedDisbursementTotal = computed(() => new Intl.NumberFormat('en-BD', {
  maximumFractionDigits: 0,
}).format(disbursementTotal.value))
const formatAmount = (value: number) => new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(value)
const rowsByView = computed(() => {
  const portfolio = dashboardMetrics.value
  if (!portfolio) return []
  switch (selectedViewBy.value) {
    case 'Region':
      return portfolio.regionWiseBranchProtfolioList ?? []
    case 'Area':
      return portfolio.areaWiseBranchProtfolioList ?? []
    case 'Branch':
      return portfolio.branchProtfolioList ?? []
    default:
      return portfolio.zoneWiseBranchProtfolioList ?? []
  }
})
function getPortfolioRowLabel(row: PortfolioRow) {
  switch (selectedViewBy.value) {
    case 'Region':
      return row.region || [row.regionName, row.regionCode ? `(${row.regionCode})` : ''].filter(Boolean).join(' ')
    case 'Area':
      return row.area || [row.areaName, row.areaCode ? `(${row.areaCode})` : ''].filter(Boolean).join(' ')
    case 'Branch':
      return row.branch || [row.branchName, row.branchCode ? `(${row.branchCode})` : ''].filter(Boolean).join(' ')
    default:
      return row.zone || [row.zoneName, row.zoneCode ? `(${row.zoneCode})` : ''].filter(Boolean).join(' ')
  }
}
const viewDisbursementSeries = computed(() => rowsByView.value
  .filter(zone => Number.isFinite(zone.disbursement))
  .map(row => ({
    label: getPortfolioRowLabel(row) || selectedViewBy.value,
    value: row.disbursement,
  }))
  .sort((left, right) => right.value - left.value))
const maximumViewDisbursement = computed(() => Math.max(...viewDisbursementSeries.value.map(zone => zone.value), 0))
const chartLabels = computed(() => {
  const series = dailyDisbursementSeries.value
  if (series.length <= 7) return series.map(item => item.label)
  return Array.from({ length: 7 }, (_, index) => series[Math.round(index * (series.length - 1) / 6)].label)
})
const disbursementValues = computed(() => dailyDisbursementSeries.value.map(item => item.value))
const chartMaximum = computed(() => {
  const maximum = Math.max(...disbursementValues.value, 0)
  if (maximum === 0) return 1
  const magnitude = 10 ** Math.floor(Math.log10(maximum))
  return Math.ceil(maximum / magnitude) * magnitude
})
const channelRows = [
  { name: 'Website', value: 42, color: '#f59e0b' },
  { name: 'Mobile', value: 27, color: '#fbbf24' },
  { name: 'Partner', value: 15, color: '#fcd34d' },
  { name: 'Retail', value: 9, color: '#f59e0b' },
  { name: 'Wholesale', value: 5, color: '#b45309' },
]
const anomalies = [
  { metric: 'Revenue', date: 'May 29, 2025', impact: 'High', confidence: '92%', severity: 'high' },
  { metric: 'Accounts', date: 'May 24, 2025', impact: 'Medium', confidence: '86%', severity: 'medium' },
  { metric: 'Traffic', date: 'May 18, 2025', impact: 'Medium', confidence: '76%', severity: 'medium' },
  { metric: 'Support', date: 'May 12, 2025', impact: 'Low', confidence: '65%', severity: 'low' },
  { metric: 'Marketing', date: 'May 7, 2025', impact: 'Low', confidence: '60%', severity: 'low' },
]

const selectedSegments = ref(['Non-borrower members', 'Current borrowers', 'OD borrowers'])
const datePickerOpen = ref(false)
const filtersOpen = ref(false)
const memberTotal = computed(() => dashboardMetrics.value?.totalMember ?? 0)
const borrowerTotal = computed(() => dashboardMetrics.value?.totalBorrower ?? 0)
const overdueBorrowerTotal = computed(() => dashboardMetrics.value?.totalOverdueBorrower ?? 0)
const formatWholeNumber = (value: number) => new Intl.NumberFormat('en-BD', { maximumFractionDigits: 0 }).format(value)
const memberSegments = computed(() => {
  const totalMembers = memberTotal.value
  const totalBorrowers = Math.min(borrowerTotal.value, totalMembers)
  const overdueBorrowers = Math.min(overdueBorrowerTotal.value, totalBorrowers)
  const segments = [
    { label: 'Non-borrower members', count: totalMembers - totalBorrowers, color: '#3b82f6' },
    { label: 'Current borrowers', count: totalBorrowers - overdueBorrowers, color: '#f97316' },
    { label: 'OD borrowers', count: overdueBorrowers, color: '#a78bfa' },
  ]
  return segments.map(segment => ({
    ...segment,
    percentage: totalMembers > 0 ? segment.count / totalMembers * 100 : 0,
  }))
})
const visibleSegments = computed(() => memberSegments.value.filter(segment => selectedSegments.value.includes(segment.label)))
const formattedDateRange = computed(() => {
  if (!dateRangeStart.value || !dateRangeEnd.value) return 'Select date range'
  const start = new Date(`${dateRangeStart.value}T00:00:00`)
  const end = new Date(`${dateRangeEnd.value}T00:00:00`)
  const monthDay = new Intl.DateTimeFormat('en', { month: 'short', day: 'numeric' })
  const year = new Intl.DateTimeFormat('en', { year: 'numeric' })
  return `${monthDay.format(start)} – ${monthDay.format(end)}, ${year.format(end)}`
})

const chartPlot = { left: 42, top: 12, width: 650, height: 204 }
function disbursementPoint(value: number, index: number) {
  const series = disbursementValues.value
  const x = chartPlot.left + (series.length <= 1 ? 0 : index / (series.length - 1) * chartPlot.width)
  const y = chartPlot.top + chartPlot.height * (1 - value / chartMaximum.value)
  return { x, y }
}
const disbursementLinePath = computed(() => disbursementValues.value.map((value, index) => {
  const point = disbursementPoint(value, index)
  return `${index === 0 ? 'M' : 'L'}${point.x.toFixed(1)},${point.y.toFixed(1)}`
}).join(' '))
const disbursementAreaPath = computed(() => {
  const points = disbursementValues.value.map((value, index) => disbursementPoint(value, index))
  if (points.length === 0) return ''
  const lastPoint = points[points.length - 1]
  return `${disbursementLinePath.value} L${lastPoint.x.toFixed(1)},${(chartPlot.top + chartPlot.height).toFixed(1)} L${points[0].x.toFixed(1)},${(chartPlot.top + chartPlot.height).toFixed(1)} Z`
})
const chartAxisLabels = computed(() => [4, 3, 2, 1, 0].map(fraction => (
  new Intl.NumberFormat('en-BD', { notation: 'compact', maximumFractionDigits: 1 })
    .format(chartMaximum.value * fraction / 4)
)))

const donutSegments = computed(() => {
  let cumulative = 0
  const visibleTotal = visibleSegments.value.reduce((total, segment) => total + segment.count, 0)
  if (visibleTotal === 0) return '#253247 0% 100%'
  return visibleSegments.value.map(segment => {
    const start = cumulative
    cumulative += segment.count / visibleTotal * 100
    const end = cumulative
    return `${segment.color} ${start}% ${end}%`
  }).join(', ')
})
</script>

<template>
  <main class="shell page-dashboard">
    <div class="dashboard-shell">
      <div class="dashboard-toolbar">
        <div class="toolbar-popover-wrap">
          <button
            class="dashboard-toolbar-button date-range-button"
            type="button"
            :aria-expanded="datePickerOpen"
            @click="datePickerOpen = !datePickerOpen; filtersOpen = false"
          >
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <rect x="3.5" y="5" width="17" height="15.5" rx="2" stroke="currentColor" stroke-width="1.6" />
              <path d="M7.5 3.5v3M16.5 3.5v3M3.5 9.5h17" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" />
            </svg>
            <span>{{ formattedDateRange }}</span>
            <svg class="toolbar-chevron" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="m7 10 5 5 5-5" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" /></svg>
          </button>
          <div v-if="datePickerOpen" class="dashboard-popover date-range-popover">
            <label>Start date<input v-model="dateRangeStart" type="date" :max="dateRangeEnd || undefined" /></label>
            <label>End date<input v-model="dateRangeEnd" type="date" :min="dateRangeStart || undefined" /></label>
            <button class="toolbar-done-button" type="button" :disabled="dashboardLoading" @click="datePickerOpen = false; void loadDashboard()">Done</button>
          </div>
        </div>
        <div class="toolbar-popover-wrap">
          <button
            class="dashboard-toolbar-button filters-button"
            type="button"
            :aria-expanded="filtersOpen"
            @click="filtersOpen = !filtersOpen; datePickerOpen = false"
          >
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M4 6h16M7 12h10m-7 6h4" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" /></svg>
            <span>Filters</span>
            <svg class="toolbar-chevron" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="m7 10 5 5 5-5" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" /></svg>
          </button>
          <div v-if="filtersOpen" class="dashboard-popover filters-popover">
            <label class="view-by-filter">
              <span>View By</span>
              <select v-model="selectedViewBy" :disabled="dashboardLoading" aria-label="View by">
                <option v-for="option in viewByOptions" :key="option" :value="option">{{ option }}</option>
              </select>
            </label>
            <button
              class="toolbar-done-button view-by-apply-button"
              type="button"
              :disabled="dashboardLoading"
              @click="filtersOpen = false; void loadDashboard()"
            >
              Apply
            </button>
            <div class="filter-popover-heading">
              <strong>Member segments</strong>
              <button type="button" @click="selectedSegments = memberSegments.map(segment => segment.label)">Reset</button>
            </div>
            <label v-for="segment in memberSegments" :key="segment.label" class="segment-filter-option">
              <input v-model="selectedSegments" type="checkbox" :value="segment.label" />
              <span class="legend-swatch" :style="{ background: segment.color }" />
              <span>{{ segment.label }}</span>
            </label>
          </div>
        </div>
      </div>
      <p v-if="dashboardError" class="error" role="alert">{{ dashboardError }}</p>
      <div class="stats-grid">
        <article v-for="card in topCards" :key="card.label" class="metric-card" :class="`metric-${card.tone}`">
          <div class="metric-header">
            <span class="metric-label">{{ card.label }}</span>
            <span class="metric-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" fill="none">
                <path d="M5 13.5 9.5 9l3 3L19 5.5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                <path d="M15 5.5h4v4" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
              </svg>
            </span>
          </div>
          <div class="metric-value">{{ card.value }}</div>
          <div class="metric-delta">{{ dashboardLoading ? 'Loading…' : formattedDateRange }}</div>
        </article>
      </div>

      <section class="hero-analytics">
        <div class="chart-panel">
          <div class="panel-header">
            <div class="chart-title-wrap">
              <span class="panel-title">Daily Disbursement</span>
              <span class="chart-info" role="img" aria-label="Daily disbursement for the selected date range">i</span>
            </div>
            <div class="chart-controls">
              <div class="chart-mode-controls" aria-label="Chart type">
                <button type="button" :class="{ active: chartMode === 'line' }" aria-label="Line chart" @click="chartMode = 'line'">
                  <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M4 18.5h16M5.5 15l4-4 3 2.5 6-7" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" /></svg>
                </button>
                <button type="button" :class="{ active: chartMode === 'area' }" aria-label="Area chart" @click="chartMode = 'area'">
                  <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M4 18.5h16M5 16l4-4 3 2.5 6-7v11H5Z" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" /></svg>
                </button>
              </div>
              <button class="chart-more-button" type="button" aria-label="More chart options" title="More chart options">⋮</button>
            </div>
          </div>

          <div class="disbursement-summary">
            <span>Total disbursement</span>
            <strong>{{ dashboardLoading ? 'Loading…' : formattedDisbursementTotal }}</strong>
          </div>
          <div class="chart-legend" aria-hidden="true">
            <span><i class="legend-dot disbursement-dot" />Daily disbursement (BDT)</span>
          </div>
          <div v-if="dailyDisbursementSeries.length" class="chart-surface" :class="`chart-mode-${chartMode}`" role="img" aria-label="Daily disbursement chart">
            <div class="chart-y-labels" aria-hidden="true">
              <span v-for="(label, index) in chartAxisLabels" :key="index">{{ label }}</span>
            </div>
            <svg class="disbursement-plot" viewBox="0 0 710 240" preserveAspectRatio="none" aria-hidden="true">
              <defs>
                <linearGradient id="disbursement-area-fill" x1="0" x2="0" y1="0" y2="1">
                  <stop offset="0%" stop-color="#f97316" stop-opacity=".2" />
                  <stop offset="100%" stop-color="#f97316" stop-opacity=".01" />
                </linearGradient>
              </defs>
              <g class="plot-grid">
                <line v-for="fraction in [1, 0.75, 0.5, 0.25, 0]" :key="fraction" x1="42" :y1="12 + 204 * (1 - fraction)" x2="692" :y2="12 + 204 * (1 - fraction)" />
              </g>
              <path v-if="chartMode === 'area'" :d="disbursementAreaPath" fill="url(#disbursement-area-fill)" />
              <path :d="disbursementLinePath" class="disbursement-line" />
              <circle
                v-for="(value, index) in disbursementValues"
                :key="index"
                :cx="disbursementPoint(value, index).x"
                :cy="disbursementPoint(value, index).y"
                r="3.2"
              />
            </svg>
            <div class="chart-labels">
              <span v-for="label in chartLabels" :key="label">{{ label }}</span>
            </div>
          </div>
          <p v-else class="chart-empty-state">
            {{ dailyDisbursementError || (dailyDisbursementLoading ? 'Loading daily disbursement data…' : 'No date-wise disbursement data for this date range.') }}
          </p>
          <p v-if="dailyDisbursementError && dailyDisbursementSeries.length" class="chart-empty-state" role="alert">
            {{ dailyDisbursementError }}
          </p>
        </div>

        <aside class="insight-panel">
          <div class="insight-header">
            <span class="insight-title"><span class="insight-sparkle" aria-hidden="true">✦</span> Disbursement by {{ selectedViewBy }}</span>
            <span class="impact-tag">Selected period</span>
          </div>
          <h3>{{ formattedDisbursementTotal }} BDT total</h3>
          <p>{{ selectedViewBy }} totals for {{ formattedDateRange }}.</p>
          <div v-if="viewDisbursementSeries.length" class="zone-disbursement-list">
            <div v-for="zone in viewDisbursementSeries" :key="zone.label" class="zone-disbursement-row">
              <div class="zone-disbursement-heading">
                <span>{{ zone.label }}</span>
                <strong>{{ formatAmount(zone.value) }} BDT</strong>
              </div>
              <div class="zone-disbursement-track">
                <span :style="{ width: `${maximumViewDisbursement > 0 ? zone.value / maximumViewDisbursement * 100 : 0}%` }" />
              </div>
            </div>
          </div>
          <p v-else>No zone data is available for this period.</p>
        </aside>
      </section>

      <section class="bottom-grid">
        <article class="mini-panel">
          <div class="mini-panel-header">
            <span>Revenue by Channel</span>
          </div>
          <div class="channel-list">
            <div v-for="row in channelRows" :key="row.name" class="channel-row">
              <div class="channel-label">{{ row.name }}</div>
              <div class="channel-bar">
                <span :style="{ width: `${row.value}%`, background: row.color }" />
              </div>
              <div class="channel-value">{{ row.value }}%</div>
            </div>
          </div>
        </article>

        <article class="mini-panel">
          <div class="mini-panel-header">
            <span>Member Segment</span>
            <span class="chart-info" role="img" aria-label="Member distribution by segment">i</span>
          </div>
          <div class="member-segment-summary">
            <span>Borrower <strong>{{ formatWholeNumber(borrowerTotal) }}</strong></span>
            <span>OD Borrower <strong>{{ formatWholeNumber(overdueBorrowerTotal) }}</strong></span>
          </div>
          <div class="donut-wrap">
            <div
              class="donut-chart"
              role="img"
              :aria-label="`Members by segment: ${visibleSegments.map(item => `${item.label} ${formatWholeNumber(item.count)}, ${item.percentage.toFixed(1)} percent`).join(', ')}`"
              :style="{ background: `conic-gradient(${donutSegments})` }"
            >
              <div class="donut-inner">
                <strong>{{ formatWholeNumber(memberTotal) }}</strong>
                <span>MEMBERS</span>
              </div>
            </div>
            <div class="legend-list">
              <div v-for="item in visibleSegments" :key="item.label" class="legend-item">
                <span class="legend-swatch" :style="{ background: item.color }" />
                <span>{{ item.label }}</span>
                <strong>{{ formatWholeNumber(item.count) }} · {{ item.percentage.toFixed(1) }}%</strong>
              </div>
            </div>
          </div>
        </article>

        <article class="mini-panel">
          <div class="mini-panel-header">
            <span>Recent Anomalies</span>
            <button type="button" class="text-link">View All</button>
          </div>
          <div class="anomaly-table">
            <div class="anomaly-head">
              <span>Metric</span>
              <span>Date</span>
              <span>Impact</span>
              <span>AI Confidence</span>
            </div>
            <div v-for="(item, index) in anomalies" :key="`${item.metric}-${index}`" class="anomaly-row">
              <span>{{ item.metric }}</span>
              <span>{{ item.date }}</span>
              <span :class="`impact-${item.severity}`">{{ item.impact }}</span>
              <span>{{ item.confidence }}</span>
            </div>
          </div>
        </article>
      </section>
    </div>
  </main>
</template>
