<script setup lang="ts">
import { computed, ref } from 'vue'

type ProposalStatus = 'Approved' | 'In review' | 'Documents pending' | 'Declined'
type Proposal = {
  id: string
  applicant: string
  product: string
  requestedAmount: number
  submittedAt: string
  status: ProposalStatus
  riskScore: number | null
}

const proposals: Proposal[] = [
  { id: 'LP-26-1048', applicant: 'Borrower 1048', product: 'SME Term Loan', requestedAmount: 1250000, submittedAt: '2026-11-09', status: 'In review', riskScore: 61 },
  { id: 'LP-26-1047', applicant: 'Borrower 1047', product: 'Personal Loan', requestedAmount: 350000, submittedAt: '2026-11-08', status: 'Approved', riskScore: 28 },
  { id: 'LP-26-1046', applicant: 'Borrower 1046', product: 'Agriculture Loan', requestedAmount: 580000, submittedAt: '2026-11-07', status: 'Documents pending', riskScore: null },
  { id: 'LP-26-1045', applicant: 'Borrower 1045', product: 'SME Term Loan', requestedAmount: 2100000, submittedAt: '2026-11-06', status: 'Approved', riskScore: 34 },
  { id: 'LP-26-1044', applicant: 'Borrower 1044', product: 'Personal Loan', requestedAmount: 240000, submittedAt: '2026-11-05', status: 'Declined', riskScore: 82 },
  { id: 'LP-26-1043', applicant: 'Borrower 1043', product: 'Small Business Loan', requestedAmount: 940000, submittedAt: '2026-11-04', status: 'In review', riskScore: 56 },
  { id: 'LP-26-1042', applicant: 'Borrower 1042', product: 'Agriculture Loan', requestedAmount: 425000, submittedAt: '2026-11-03', status: 'Approved', riskScore: 31 },
  { id: 'LP-26-1041', applicant: 'Borrower 1041', product: 'Personal Loan', requestedAmount: 180000, submittedAt: '2026-11-02', status: 'Documents pending', riskScore: null },
]

const searchQuery = ref('')
const statusFilter = ref<'All statuses' | ProposalStatus>('All statuses')
const statuses: ProposalStatus[] = ['Approved', 'In review', 'Documents pending', 'Declined']
const filteredProposals = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  return proposals.filter(proposal => {
    const matchesStatus = statusFilter.value === 'All statuses' || proposal.status === statusFilter.value
    const matchesQuery = !query || `${proposal.id} ${proposal.applicant} ${proposal.product}`.toLowerCase().includes(query)
    return matchesStatus && matchesQuery
  })
})
const approvedCount = computed(() => proposals.filter(proposal => proposal.status === 'Approved').length)
const assessedProposals = computed(() => proposals.filter(proposal => proposal.riskScore !== null))
const averageRisk = computed(() => {
  if (!assessedProposals.value.length) return 0
  return Math.round(assessedProposals.value.reduce((sum, proposal) => sum + (proposal.riskScore ?? 0), 0) / assessedProposals.value.length)
})
const totalRequested = computed(() => proposals.reduce((sum, proposal) => sum + proposal.requestedAmount, 0))
const statusCounts = computed(() => statuses.map(status => ({
  label: status,
  count: proposals.filter(proposal => proposal.status === status).length,
  percent: Math.round(proposals.filter(proposal => proposal.status === status).length / proposals.length * 100),
})))

function formatCurrency(amount: number) {
  return new Intl.NumberFormat('en-BD', { style: 'currency', currency: 'BDT', maximumFractionDigits: 0 }).format(amount)
}

function formatDate(date: string) {
  return new Intl.DateTimeFormat('en-BD', { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(`${date}T00:00:00`))
}

function riskLabel(score: number | null) {
  if (score === null) return 'Not assessed'
  if (score >= 70) return 'High'
  if (score >= 40) return 'Moderate'
  return 'Low'
}
</script>

<template>
  <main class="shell admin-shell loan-analysis-page" aria-labelledby="loan-analysis-title">
    <header class="loan-analysis-heading">
      <div>
        <p class="eyebrow">CREDIT PORTFOLIO</p>
        <h1 id="loan-analysis-title">Loan Proposal &amp; Assessment Analysis</h1>
        <p class="hint">Review proposal flow, assessment coverage, and risk indicators.</p>
      </div>
      <span class="sample-data-badge"><span aria-hidden="true"></span>Sample data · prototype</span>
    </header>

    <section class="loan-kpi-grid" aria-label="Portfolio summary">
      <article class="loan-kpi-card">
        <span>Total proposals</span>
        <strong>{{ proposals.length }}</strong>
        <small>In the sample portfolio</small>
      </article>
      <article class="loan-kpi-card">
        <span>Approved</span>
        <strong>{{ approvedCount }}</strong>
        <small>{{ Math.round(approvedCount / proposals.length * 100) }}% of proposals</small>
      </article>
      <article class="loan-kpi-card">
        <span>Assessment coverage</span>
        <strong>{{ assessedProposals.length }} / {{ proposals.length }}</strong>
        <small>Have a recorded risk score</small>
      </article>
      <article class="loan-kpi-card">
        <span>Average risk score</span>
        <strong>{{ averageRisk }}<small class="score-suffix"> / 100</small></strong>
        <small>Assessed proposals only</small>
      </article>
      <article class="loan-kpi-card">
        <span>Requested amount</span>
        <strong class="amount-value">{{ formatCurrency(totalRequested) }}</strong>
        <small>Across all sample proposals</small>
      </article>
    </section>

    <section class="loan-analysis-overview">
      <article class="loan-analysis-panel status-panel">
        <div class="loan-panel-heading">
          <div>
            <h2>Proposal status</h2>
            <p>Current stage across the sample portfolio</p>
          </div>
          <span class="loan-panel-count">{{ proposals.length }} total</span>
        </div>
        <div class="proposal-status-list">
          <div v-for="status in statusCounts" :key="status.label" class="proposal-status-row">
            <div class="proposal-status-label">
              <span :class="['status-dot', `status-dot-${status.label.toLowerCase().replace(/ /g, '-')}`]" />
              <span>{{ status.label }}</span>
              <strong>{{ status.count }}</strong>
            </div>
            <div class="status-track" role="img" :aria-label="`${status.label}: ${status.percent}%`">
              <span :class="`status-bar-${status.label.toLowerCase().replace(/ /g, '-')}`" :style="{ width: `${status.percent}%` }" />
            </div>
          </div>
        </div>
      </article>

      <article class="loan-analysis-panel assessment-panel">
        <div class="loan-panel-heading">
          <div>
            <h2>Assessment snapshot</h2>
            <p>Risk score bands for assessed proposals</p>
          </div>
        </div>
        <div class="assessment-score">
          <strong>{{ averageRisk }}</strong>
          <span>Average score<br>out of 100</span>
        </div>
        <div class="risk-legend">
          <span><i class="risk-low"></i>Low <strong>{{ assessedProposals.filter(proposal => (proposal.riskScore ?? 0) < 40).length }}</strong></span>
          <span><i class="risk-moderate"></i>Moderate <strong>{{ assessedProposals.filter(proposal => (proposal.riskScore ?? 0) >= 40 && (proposal.riskScore ?? 0) < 70).length }}</strong></span>
          <span><i class="risk-high"></i>High <strong>{{ assessedProposals.filter(proposal => (proposal.riskScore ?? 0) >= 70).length }}</strong></span>
        </div>
        <p class="assessment-disclaimer">Illustrative scores only. Not a credit decision or recommendation.</p>
      </article>
    </section>

    <section class="loan-analysis-panel proposal-table-panel">
      <div class="loan-panel-heading proposal-table-heading">
        <div>
          <h2>Recent proposals</h2>
          <p>Search and filter the sample applications.</p>
        </div>
        <div class="proposal-table-filters">
          <label class="sr-only" for="proposal-search">Search proposals</label>
          <input id="proposal-search" v-model="searchQuery" type="search" placeholder="Search ID, borrower, product" />
          <label class="sr-only" for="proposal-status">Filter by status</label>
          <select id="proposal-status" v-model="statusFilter">
            <option>All statuses</option>
            <option v-for="status in statuses" :key="status">{{ status }}</option>
          </select>
        </div>
      </div>
      <div class="loan-table-scroll">
        <table class="loan-proposal-table">
          <thead>
            <tr>
              <th scope="col">Proposal ID</th>
              <th scope="col">Borrower</th>
              <th scope="col">Loan product</th>
              <th scope="col">Requested amount</th>
              <th scope="col">Submitted</th>
              <th scope="col">Risk score</th>
              <th scope="col">Status</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="proposal in filteredProposals" :key="proposal.id">
              <td class="proposal-id">{{ proposal.id }}</td>
              <td>{{ proposal.applicant }}</td>
              <td>{{ proposal.product }}</td>
              <td>{{ formatCurrency(proposal.requestedAmount) }}</td>
              <td>{{ formatDate(proposal.submittedAt) }}</td>
              <td>
                <span :class="['risk-score', `risk-score-${riskLabel(proposal.riskScore).toLowerCase().replace(' ', '-')}`]">
                  {{ proposal.riskScore ?? '—' }}<small v-if="proposal.riskScore !== null"> / 100</small>
                </span>
              </td>
              <td><span :class="['proposal-status-pill', `proposal-status-${proposal.status.toLowerCase().replace(/ /g, '-')}`]">{{ proposal.status }}</span></td>
            </tr>
            <tr v-if="filteredProposals.length === 0">
              <td colspan="7" class="proposal-empty-state">No proposals match your search and status filter.</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p class="proposal-table-note">Sample records for layout preview only. Connect an approved loan data source before using for operational decisions.</p>
    </section>
  </main>
</template>
