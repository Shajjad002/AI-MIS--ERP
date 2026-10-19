<script setup lang="ts">
import { computed } from 'vue'
import type { QueryResult } from '../../types/copilot'

const props = defineProps<{ result: QueryResult }>()
const numericColumns = computed(() => props.result.columns.filter(column =>
  props.result.rows.some(row => typeof row[column] === 'number' && Number.isFinite(row[column] as number)),
))
const total = computed(() => numericColumns.value.length
  ? props.result.rows.reduce((sum, row) => sum + Number(row[numericColumns.value[0]] || 0), 0)
  : 0)
const highest = computed(() => numericColumns.value.length
  ? Math.max(...props.result.rows.map(row => Number(row[numericColumns.value[0]] || 0)))
  : 0)
function formatValue(value: unknown) {
  if (typeof value === 'number') return new Intl.NumberFormat().format(value)
  return value === null || value === undefined ? '—' : String(value)
}
</script>

<template>
  <div class="summary-card">
    <p class="eyebrow">Result summary</p>
    <p>{{ result.summary }}</p>
  </div>
  <div v-if="numericColumns.length" class="kpis">
    <article class="kpi"><span>Rows</span><strong>{{ result.rows.length }}</strong></article>
    <article class="kpi"><span>{{ numericColumns[0] }}</span><strong>{{ formatValue(total) }}</strong></article>
    <article class="kpi"><span>Highest value</span><strong>{{ formatValue(highest) }}</strong></article>
  </div>
</template>
