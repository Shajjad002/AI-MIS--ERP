export type ChartType = 'bar' | 'line' | 'pie' | 'donut' | 'table' | null

export type Interpretation = {
  intent: string
  metrics: string[]
  dimensions: string[]
  chartType: ChartType
  needsClarification: boolean
  clarificationQuestion: string | null
  proposedSql: string | null
}

export type QueryResult = {
  columns: string[]
  rows: Array<Record<string, unknown>>
  executionTimeMilliseconds: number
  summary: string
}

export type QueryResponse = {
  interpretation: Interpretation
  result: QueryResult | null
}
