<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { apiFetch } from '../config/api'

type DocumentInfo = { id: string; fileName: string; contentType: string; length: number; uploadedAt: string }
type Citation = { documentId: string; fileName: string; pageNumber: number | null; section: string | null; quote: string }
type Answer = { answer: string; citations: Citation[] }

const documents = ref<DocumentInfo[]>([])
const selectedFile = ref<File | null>(null)
const question = ref('')
const answer = ref<Answer | null>(null)
const busy = ref(false)
const error = ref('')
const notice = ref('')
const token = () => localStorage.getItem('ai-mis.access-token') ?? ''

async function loadDocuments() {
  const response = await apiFetch('/api/documents', {
    headers: { Authorization: `Bearer ${token()}` },
  })
  if (!response.ok) throw new Error(`Unable to load documents (HTTP ${response.status}).`)
  documents.value = await response.json()
}
async function upload() {
  if (!selectedFile.value) { error.value = 'Choose a document first.'; return }
  busy.value = true; error.value = ''; notice.value = ''
  try {
    const data = new FormData()
    data.append('file', selectedFile.value)
    const response = await apiFetch('/api/documents/upload', {
      method: 'POST',
      headers: { Authorization: `Bearer ${token()}` },
      body: data,
    })
    const body = await response.json().catch(() => ({}))
    if (!response.ok) throw new Error(body.message ?? body.error ?? `Upload failed (HTTP ${response.status}).`)
    notice.value = `${selectedFile.value.name} uploaded and indexed into ${body.indexedChunks} searchable passages.`
    selectedFile.value = null
    await loadDocuments()
  } catch (exception) { error.value = exception instanceof Error ? exception.message : 'Upload failed.' }
  finally { busy.value = false }
}
async function ask() {
  if (!question.value.trim()) { error.value = 'Enter a question about the uploaded documents.'; return }
  busy.value = true; error.value = ''; answer.value = null
  try {
    const response = await apiFetch('/api/documents/ask', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token()}` },
      body: JSON.stringify({ question: question.value.trim() }),
    })
    const body = await response.json().catch(() => ({}))
    if (!response.ok) throw new Error(body.message ?? `Question failed (HTTP ${response.status}).`)
    answer.value = body
  } catch (exception) { error.value = exception instanceof Error ? exception.message : 'Unable to answer the question.' }
  finally { busy.value = false }
}
function formatSize(bytes: number) { return new Intl.NumberFormat().format(bytes / 1024) + ' KB' }
onMounted(() => loadDocuments().catch(exception => { error.value = exception instanceof Error ? exception.message : 'Unable to load documents.' }))
</script>

<template>
  <main class="shell admin-shell documents-page">
    <p class="eyebrow">Knowledge base</p>
    <h1>Documents & business rules</h1>
    <p class="lede">Upload reference files, then ask questions answered from indexed content with source citations.</p>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <p v-if="notice" class="notice" role="status">{{ notice }}</p>
    <section class="rag-grid">
      <form class="query-card rag-card" @submit.prevent="upload">
        <h2>Upload document</h2>
        <p class="hint">PDF, DOCX, TXT, and Markdown · maximum 10 MB</p>
        <label for="rag-document">Choose a file</label>
        <input id="rag-document" type="file" accept=".pdf,.docx,.txt,.md" @change="selectedFile = ($event.target as HTMLInputElement).files?.[0] ?? null" />
        <button :disabled="busy" type="submit">{{ busy ? 'Working…' : 'Upload and index' }}</button>
      </form>
      <form class="query-card rag-card" @submit.prevent="ask">
        <h2>Ask the knowledge base</h2>
        <label for="rag-question">Question</label>
        <textarea id="rag-question" v-model="question" rows="4" placeholder="Ask about a policy, process, or business rule…" />
        <button :disabled="busy" type="submit">{{ busy ? 'Searching…' : 'Ask with sources' }}</button>
      </form>
    </section>
    <section v-if="answer" class="results rag-answer">
      <div class="result-heading"><h2>Grounded answer</h2><span>{{ answer.citations.length }} source(s)</span></div>
      <p class="rag-answer-text">{{ answer.answer }}</p>
      <div v-if="answer.citations.length" class="citation-list">
        <h3>Sources</h3>
        <article v-for="(citation, index) in answer.citations" :key="`${citation.documentId}-${index}`" class="citation">
          <strong>[{{ index + 1 }}] {{ citation.fileName }}</strong>
          <span v-if="citation.pageNumber">Page {{ citation.pageNumber }}</span>
          <span v-if="citation.section">{{ citation.section }}</span>
          <p>{{ citation.quote }}</p>
        </article>
      </div>
    </section>
    <section class="table-card rag-documents">
      <div class="card-heading"><h2>Indexed documents</h2><button class="secondary-button" type="button" @click="loadDocuments().catch(exception => error = exception.message)">Refresh</button></div>
      <p v-if="documents.length === 0" class="hint">No documents uploaded yet.</p>
      <div v-else class="table-scroll"><table><thead><tr><th>File</th><th>Size</th><th>Uploaded</th></tr></thead><tbody><tr v-for="document in documents" :key="document.id"><td>{{ document.fileName }}</td><td>{{ formatSize(document.length) }}</td><td>{{ new Date(document.uploadedAt).toLocaleString() }}</td></tr></tbody></table></div>
    </section>
  </main>
</template>
