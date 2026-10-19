<script setup lang="ts">
import { computed } from 'vue'
import type { ChartType, QueryResult } from '../../types/copilot'

const props = defineProps<{ result: QueryResult; chartType: ChartType }>()
const numericColumns = computed(() => props.result.columns.filter(column =>
  props.result.rows.some(row => typeof row[column] === 'number' && Number.isFinite(row[column] as number)),
))
const labelColumn = computed(() => props.result.columns.find(column => !numericColumns.value.includes(column)) ?? '')
const chartRows = computed(() => {
  if (!numericColumns.value.length) return []
  const valueColumn = numericColumns.value[0]
  const values = props.result.rows.map(row => Number(row[valueColumn]) || 0)
  const maximum = Math.max(...values, 1)
  return props.result.rows.map((row, index) => ({
    label: String(row[labelColumn.value] ?? index + 1),
    value: values[index],
    percentage: (values[index] / maximum) * 100,
  }))
})
const total = computed(() => chartRows.value.reduce((sum, row) => sum + Math.max(row.value, 0), 0))
const gradient = computed(() => {
  if (!total.value) return '#23446e 0 100%'
  let start = 0
  return chartRows.value.map((row, index) => {
    const end = start + Math.max(row.value, 0) / total.value * 100
    const segment = `${['#67e8f9', '#818cf8', '#f0abfc', '#86efac', '#fcd34d'][index % 5]} ${start}% ${end}%`
    start = end
    return segment
  }).join(', ')
})
function formatValue(value: number) { return new Intl.NumberFormat().format(value) }
</script>

<template>
  <div v-if="chartType !== 'table' && chartRows.length" class="visual-card">
    <div class="card-heading"><h3>{{ chartType }} view</h3><span>{{ numericColumns[0] }}</span></div>
    <div v-if="chartType === 'bar' || chartType === 'line'" class="bar-chart">
      <div v-for="row in chartRows" :key="row.label" class="bar-row">
        <span class="bar-label">{{ row.label }}</span>
        <span class="bar-track"><span class="bar-fill" :style="{ width: `${row.percentage}%` }" /></span>
        <strong>{{ formatValue(row.value) }}</strong>
      </div>
    </div>
    <div v-else class="donut-chart">
      <div class="donut" :style="{ background: `conic-gradient(${gradient})` }"><strong>{{ formatValue(total) }}</strong></div>
      <span>{{ chartRows.length }} categories</span>
    </div>
  </div>
</template>
