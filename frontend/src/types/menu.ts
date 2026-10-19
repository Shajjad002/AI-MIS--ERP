export type MenuPageKey = 'home' | 'visualization' | 'documents' | 'users'

export type MenuItem = {
  id: string
  label: string
  section: string
  pageKey: MenuPageKey
  icon: string
  sortOrder: number
  isEnabled: boolean
  roleNames: string[]
}

export type MenuWriteRequest = Omit<MenuItem, 'id'>
