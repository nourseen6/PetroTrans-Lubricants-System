import { useEffect, useRef, useState, type KeyboardEvent, type ReactNode, type Ref } from 'react'

type TypeaheadProps<T> = {
  items: T[]
  placeholder: string
  disabled?: boolean
  autoFocus?: boolean
  query: string
  onQuery: (value: string) => void
  onSelect: (item: T) => void
  renderItem: (item: T) => ReactNode
  itemKey: (item: T) => string
  emptyText?: string
  inputRef?: Ref<HTMLInputElement>
}

export function Typeahead<T>({
  items,
  placeholder,
  disabled,
  autoFocus,
  query,
  onQuery,
  onSelect,
  renderItem,
  itemKey,
  emptyText,
  inputRef,
}: TypeaheadProps<T>) {
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const root = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function onDoc(event: MouseEvent) {
      if (root.current && !root.current.contains(event.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', onDoc)
    return () => document.removeEventListener('mousedown', onDoc)
  }, [])

  function choose(item: T) {
    onSelect(item)
    setOpen(false)
  }

  function onKey(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setOpen(true)
      setActive((current) => Math.min(current + 1, Math.max(items.length - 1, 0)))
    } else if (event.key === 'ArrowUp') {
      event.preventDefault()
      setActive((current) => Math.max(current - 1, 0))
    } else if (event.key === 'Enter') {
      if (open && items.length === 1) {
        event.preventDefault()
        choose(items[0])
      } else if (open && items[active]) {
        event.preventDefault()
        choose(items[active])
      }
    } else if (event.key === 'Tab' && open && items.length === 1) {
      choose(items[0])
    } else if (event.key === 'Escape') {
      setOpen(false)
    }
  }

  return (
    <div className="typeahead" ref={root}>
      <input
        ref={inputRef}
        value={query}
        placeholder={placeholder}
        disabled={disabled}
        autoFocus={autoFocus}
        autoComplete="off"
        onChange={(event) => {
          onQuery(event.target.value)
          setOpen(true)
          setActive(0)
        }}
        onFocus={() => setOpen(true)}
        onKeyDown={onKey}
      />
      {open ? (
        <div className="typeahead-menu">
          {items.length === 0 ? (
            <div className="typeahead-empty">{emptyText ?? 'لا توجد نتائج'}</div>
          ) : (
            items.map((item, index) => (
              <button
                key={itemKey(item)}
                type="button"
                className={index === active ? 'typeahead-option active' : 'typeahead-option'}
                onMouseDown={(event) => {
                  event.preventDefault()
                  choose(item)
                }}
              >
                {renderItem(item)}
              </button>
            ))
          )}
        </div>
      ) : null}
    </div>
  )
}
