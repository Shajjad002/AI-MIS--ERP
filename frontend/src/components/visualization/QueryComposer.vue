<script setup lang="ts">
defineProps<{ loading: boolean; error: string }>()
const question = defineModel<string>({ required: true })
const emit = defineEmits<{ run: [] }>()
</script>

<template>
  <section class="query-card">
    <label for="question">Business question</label>
    <textarea
      id="question"
      v-model="question"
      rows="3"
      placeholder="Show August 2026 loan collection by branch"
      @keydown.ctrl.enter="emit('run')"
    />
    <div class="query-actions">
      <span class="hint">Ctrl + Enter to run</span>
      <button :disabled="loading" type="button" @click="emit('run')">
        {{ loading ? 'Analyzing…' : 'Run query' }}
      </button>
    </div>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
  </section>
</template>
