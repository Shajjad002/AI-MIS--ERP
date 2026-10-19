<script setup lang="ts">
import { ref } from 'vue'
import ExcelReport from '../components/visualization/ExcelReport.vue'
import QueryComposer from '../components/visualization/QueryComposer.vue'
import ResultChart from '../components/visualization/ResultChart.vue'
import ResultSummary from '../components/visualization/ResultSummary.vue'
import ResultTable from '../components/visualization/ResultTable.vue'
import { useCopilotQuery } from '../composables/useCopilotQuery'

const question = ref('')
const activeReportSource = ref<'excel' | 'ai'>('excel')
const { loading, error, response, runQuery } = useCopilotQuery()
</script>

<template>
  <main class="shell analytics-page">
    <header class="hero">
      <p class="eyebrow">AI MIS & ERP Copilot</p>
      <h1>Visualization</h1>
      <p class="lede">Build graphical reports from an Excel workbook or ask the AI to query MIS data.</p>
    </header>
    <div class="report-source-tabs" role="tablist" aria-label="Report source">
      <button id="excel-report-tab" type="button" role="tab" :aria-selected="activeReportSource === 'excel'" :class="{ active: activeReportSource === 'excel' }" @click="activeReportSource = 'excel'">Excel workbook</button>
      <button id="ai-report-tab" type="button" role="tab" :aria-selected="activeReportSource === 'ai'" :class="{ active: activeReportSource === 'ai' }" @click="activeReportSource = 'ai'">Ask AI</button>
    </div>
    <ExcelReport v-if="activeReportSource === 'excel'" role="tabpanel" aria-labelledby="excel-report-tab" />
    <section v-else role="tabpanel" aria-labelledby="ai-report-tab">
      <QueryComposer v-model="question" :loading="loading" :error="error" @run="runQuery(question)" />
      <div v-if="response" class="results" aria-live="polite">
        <div class="result-heading">
          <div><p class="eyebrow">Query result</p><h2>{{ response.interpretation.intent || 'Analysis' }}</h2></div>
          <span v-if="response.result" class="execution">{{ response.result.executionTimeMilliseconds }} ms</span>
        </div>
        <div v-if="response.interpretation.needsClarification" class="notice">
          {{ response.interpretation.clarificationQuestion || 'Please clarify your question.' }}
        </div>
        <div v-else-if="!response.result" class="notice">The AI did not return an executable query.</div>
        <template v-else>
          <ResultSummary :result="response.result" />
          <ResultChart :result="response.result" :chart-type="response.interpretation.chartType" />
          <ResultTable :result="response.result" />
        </template>
      </div>
    </section>
  </main>
</template>
