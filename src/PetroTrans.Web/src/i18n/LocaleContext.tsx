import { createContext, useContext } from 'react'
import type { Locale } from './strings'
import { strings } from './strings'

export type LocaleContextValue = {
  locale: Locale
  t: (typeof strings)[Locale]
  toggleLocale: () => void
}

export const LocaleContext = createContext<LocaleContextValue | null>(null)

export function useLocale(): LocaleContextValue {
  const value = useContext(LocaleContext)
  if (!value) {
    throw new Error('LocaleContext is missing')
  }
  return value
}
