<script setup lang="ts">
import type { QueryResult } from '../../types/copilot'

defineProps<{ result: QueryResult }>()
function formatValue(value: unknown) {
  if (typeof value === 'number') return new Intl.NumberFormat().format(value)
  return value === null || value === undefined ? '—' : String(value)
}
</script>

<template>
  <div class="table-card">
    <div class="card-heading"><h3>Data table</h3><span>{{ result.rows.length }} rows</span></div>
    <div class="table-scroll">
      <table>
        <thead><tr><th v-for="column in result.columns" :key="column">{{ column }}</th></tr></thead>
        <tbody>
          <tr v-for="(row, index) in result.rows" :key="index">
            <td v-for="column in result.columns" :key="column">{{ formatValue(row[column]) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
