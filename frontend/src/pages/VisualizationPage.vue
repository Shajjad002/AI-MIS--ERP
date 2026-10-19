<script setup lang="ts">
import { ref } from 'vue'
import QueryComposer from '../components/visualization/QueryComposer.vue'
import ResultChart from '../components/visualization/ResultChart.vue'
import ResultSummary from '../components/visualization/ResultSummary.vue'
import ResultTable from '../components/visualization/ResultTable.vue'
import { useCopilotQuery } from '../composables/useCopilotQuery'

const question = ref('')
const { loading, error, response, runQuery } = useCopilotQuery()
</script>

<template>
  <main class="shell analytics-page">
    <header class="hero">
      <p class="eyebrow">AI MIS & ERP Copilot</p>
      <h1>Visualization</h1>
      <p class="lede">Ask a natural-language MIS question and review the result as a table or visual summary.</p>
    </header>
    <QueryComposer v-model="question" :loading="loading" :error="error" @run="runQuery(question)" />
    <section v-if="response" class="results" aria-live="polite">
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
    </section>
  </main>
</template>
