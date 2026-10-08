import { useEffect, useState, type FormEvent } from 'react'
import {
  addVariant,
  archiveProduct,
  createProduct,
  fetchOfficialImages,
  fetchProduct,
  fetchProducts,
  restoreProduct,
  archiveVariant,
  restoreVariant,
  updateProduct,
  updateVariant,
  type OfficialImage,
  type ProductListItem,
  type SaveVariant,
} from '../api/catalog'
import { useLocale } from '../i18n/LocaleContext'
import { ConfirmDialog, EmptyState, ErrorBanner } from '../ui/Feedback'
import { parseAmount } from '../ui/format'
import { groupByCatalogCategory } from '../ui/catalogGroups'

const emptyVariant = (): SaveVariant => ({
  packagingType: '',
  packagingSize: '',
  sku: '',
  barcode: '',
  minStock: null,
  standardWholesalePrice: null,
  isActive: true,
  standardPurchasePrice: null,
})

export function ProductsPage() {
  const { t } = useLocale()
  const [query, setQuery] = useState('')
  const [rows, setRows] = useState<ProductListItem[]>([])
  const [images, setImages] = useState<OfficialImage[]>([])
  const [open, setOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [brand, setBrand] = useState('')
  const [category, setCategory] = useState('')
  const [specification, setSpecification] = useState('')
  const [isActive, setIsActive] = useState(true)
  const [imagePath, setImagePath] = useState('')
  const [variants, setVariants] = useState<Array<SaveVariant & { id?: string }>>([emptyVariant()])
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const [showArchived, setShowArchived] = useState(false)
  const [archiveId, setArchiveId] = useState<string | null>(null)
  const [variantArchive, setVariantArchive] = useState<{ id: string; active: boolean } | null>(null)

  async function refresh(search = query) {
    const [list, official] = await Promise.all([fetchProducts(search, showArchived), fetchOfficialImages()])
    setRows(list)
    setImages(official)
  }

  useEffect(() => {
    void refresh('').catch((err: unknown) => setError(err))
  }, [])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      void refresh(query).catch((err: unknown) => setError(err))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [query, showArchived])

  function startCreate() {
    setOpen(true)
    setEditingId(null)
    setName('')
    setBrand('')
    setCategory('')
    setSpecification('')
    setIsActive(true)
    setImagePath('')
    setVariants([emptyVariant()])
    setError(null)
  }

  async function openEdit(id: string) {
    const product = await fetchProduct(id)
    setOpen(true)
    setEditingId(product.id)
    setName(product.name)
    setBrand(product.brand ?? '')
    setCategory(product.category ?? '')
    setSpecification(product.specification ?? '')
    setIsActive(product.isActive)
    setImagePath(product.imageRelativePath ?? '')
    setVariants(
      product.variants.map((item) => ({
        id: item.id,
        packagingType: item.packagingType,
        packagingSize: item.packagingSize,
        sku: item.sku ?? '',
        barcode: item.barcode ?? '',
        minStock: item.minStock,
        standardWholesalePrice: item.standardWholesalePrice,
        isActive: item.isActive,
        standardPurchasePrice: item.standardPurchasePrice ?? null,
      })),
    )
    setError(null)
  }

  async function onSave(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      if (editingId) {
        await updateProduct(editingId, {
          name,
          brand,
          category,
          specification,
          isActive,
          imageRelativePath: imagePath || null,
        })
        for (const variant of variants) {
          const payload = {
            packagingType: variant.packagingType,
            packagingSize: variant.packagingSize,
            sku: variant.sku,
            barcode: variant.barcode,
            minStock: variant.minStock,
            standardWholesalePrice: variant.standardWholesalePrice,
            standardPurchasePrice: variant.standardPurchasePrice ?? null,
            isActive: variant.isActive,
          }
          if (variant.id) {
            await updateVariant(variant.id, payload)
          } else {
            await addVariant(editingId, payload)
          }
        }
      } else {
        await createProduct({
          name,
          brand,
          category,
          specification,
          isActive,
          imageRelativePath: imagePath || null,
          variants: variants.map((variant) => ({
            packagingType: variant.packagingType,
            packagingSize: variant.packagingSize,
            sku: variant.sku,
            barcode: variant.barcode,
            minStock: variant.minStock,
            standardWholesalePrice: variant.standardWholesalePrice,
            standardPurchasePrice: variant.standardPurchasePrice ?? null,
            isActive: variant.isActive,
          })),
        })
      }
      setOpen(false)
      await refresh()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  function patchVariant(index: number, patch: Partial<SaveVariant>) {
    setVariants((current) => current.map((item, i) => (i === index ? { ...item, ...patch } : item)))
  }

  async function onArchive(id: string, currentlyActive: boolean) {
    setBusy(true)
    setError(null)
    setArchiveId(null)
    try {
      if (currentlyActive) await archiveProduct(id)
      else await restoreProduct(id)
      await refresh()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section>
      <div className="page-head">
        <div>
          <h1 className="page-title">{t.nav.products}</h1>
          <p className="muted">{t.products.hint}</p>
        </div>
        <button type="button" className="primary-btn toolbar-btn" onClick={startCreate}>
          {t.products.add}
        </button>
      </div>
      <ErrorBanner error={error} onRetry={() => void refresh()} />
      <div className="toolbar">
        <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder={t.products.search} />
        <label className="check filter-check">
          <input type="checkbox" checked={showArchived} onChange={(event) => setShowArchived(event.target.checked)} />
          <span>{t.products.showArchived}</span>
        </label>
      </div>

      {open ? (
        <form className="editor" onSubmit={onSave}>
          <div className="page-head">
            <h2 className="editor-title">{editingId ? t.products.edit : t.products.add}</h2>
            <button type="button" className="btn-secondary" onClick={() => setOpen(false)}>{t.close}</button>
          </div>
          <label className="field">
            <span>{t.products.name}</span>
            <input value={name} onChange={(event) => setName(event.target.value)} required autoFocus />
          </label>
          <div className="form-grid">
            <label className="field">
              <span>{t.products.brand}</span>
              <input value={brand} onChange={(event) => setBrand(event.target.value)} />
            </label>
            <label className="field">
              <span>{t.products.category}</span>
              <input value={category} onChange={(event) => setCategory(event.target.value)} />
            </label>
            <label className="field">
              <span>{t.products.specification}</span>
              <input value={specification} onChange={(event) => setSpecification(event.target.value)} />
            </label>
          </div>
          <label className="field">
            <span>{t.products.image}</span>
            <select value={imagePath} onChange={(event) => setImagePath(event.target.value)}>
              <option value="">{t.products.noImage}</option>
              {images.map((image) => (
                <option key={image.relativePath} value={image.relativePath}>
                  {image.relativePath.split(/[/\\]/).pop()}
                </option>
              ))}
            </select>
          </label>
          {imagePath ? <img className="product-preview" src={images.find((item) => item.relativePath === imagePath)?.url} alt="" /> : null}

          <h3 className="editor-title">{t.products.variants}</h3>
          <table className="data-table compact">
            <thead>
              <tr>
                <th>{t.products.packagingType}</th>
                <th>{t.products.packagingSize}</th>
                <th>SKU</th>
                <th>{t.products.barcode}</th>
                <th>{t.products.price}</th>
                <th>{t.products.companyPrice}</th>
                <th>{t.products.minStock}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {variants.map((variant, index) => (
                <tr key={variant.id ?? `new-${index}`}>
                  <td>
                    <input value={variant.packagingType} onChange={(event) => patchVariant(index, { packagingType: event.target.value })} required />
                  </td>
                  <td>
                    <input value={variant.packagingSize} onChange={(event) => patchVariant(index, { packagingSize: event.target.value })} required />
                  </td>
                  <td>
                    <input value={variant.sku} onChange={(event) => patchVariant(index, { sku: event.target.value })} />
                  </td>
                  <td>
                    <input value={variant.barcode} onChange={(event) => patchVariant(index, { barcode: event.target.value })} />
                  </td>
                  <td>
                    <input
                      value={variant.standardWholesalePrice ?? ''}
                      onChange={(event) => patchVariant(index, { standardWholesalePrice: parseAmount(event.target.value) })}
                      inputMode="decimal"
                    />
                  </td>
                  <td>
                    <input
                      value={variant.standardPurchasePrice ?? ''}
                      onChange={(event) => patchVariant(index, { standardPurchasePrice: parseAmount(event.target.value) })}
                      inputMode="decimal"
                    />
                  </td>
                  <td>
                    <input
                      value={variant.minStock ?? ''}
                      onChange={(event) => patchVariant(index, { minStock: parseAmount(event.target.value) })}
                      inputMode="decimal"
                    />
                  </td>
                  <td>
                    {variant.id ? (
                      <button
                        type="button"
                        className="link-btn"
                        title={variant.isActive ? t.products.archiveVariant : t.products.restoreVariant}
                        onClick={() => {
                          const id = variant.id
                          if (!id) return
                          setVariantArchive({ id, active: variant.isActive })
                        }}
                      >
                        {variant.isActive ? t.products.archive : t.products.restore}
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="action-row">
            <button type="button" className="btn-secondary" onClick={() => setVariants((current) => [...current, emptyVariant()])}>
              {t.products.addVariant}
            </button>
            <button className="primary-btn toolbar-btn" type="submit" disabled={busy}>
              {busy ? t.loading : t.save}
            </button>
          </div>
        </form>
      ) : null}

      {rows.length === 0 ? (
        <EmptyState title={t.products.empty} hint={t.products.emptyHint} actionLabel={t.products.add} onAction={startCreate} />
      ) : (
        <table className="data-table compact">
          <thead>
            <tr>
              <th>{t.products.name}</th>
              <th>{t.products.brand}</th>
              <th>{t.products.variants}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {groupByCatalogCategory(rows, (row) => row.category).flatMap((group) => [
              <tr key={`cat-${group.title}`} className="category-row">
                <td colSpan={4}>{group.title}</td>
              </tr>,
              ...group.rows.map((row) => (
                <tr key={row.id} className="clickable" onClick={() => void openEdit(row.id)}>
                  <td>
                    <span className="name-with-thumb">
                      {row.imageUrl ? <img src={row.imageUrl} alt="" /> : null}
                      {row.name}
                      {row.isActive ? '' : ` — ${t.products.archive}`}
                    </span>
                  </td>
                  <td>{row.brand ?? '—'}</td>
                  <td>{row.variantCount}</td>
                  <td>
                    <button
                      type="button"
                      className="link-btn"
                      onClick={(event) => {
                        event.stopPropagation()
                        setArchiveId(row.id)
                      }}
                    >
                      {row.isActive ? t.products.archive : t.products.restore}
                    </button>
                  </td>
                </tr>
              )),
            ])}
          </tbody>
        </table>
      )}
      {archiveId ? (
        <ConfirmDialog
          title={t.products.archiveConfirm}
          body={t.products.archiveBody}
          confirmLabel={rows.find((row) => row.id === archiveId)?.isActive ? t.products.archive : t.products.restore}
          onConfirm={() => {
            const row = rows.find((item) => item.id === archiveId)
            if (row) void onArchive(row.id, row.isActive)
          }}
          onCancel={() => setArchiveId(null)}
        />
      ) : null}
      {variantArchive ? (
        <ConfirmDialog
          title={variantArchive.active ? t.products.archiveVariant : t.products.restoreVariant}
          body={t.products.archiveBody}
          confirmLabel={variantArchive.active ? t.products.archive : t.products.restore}
          onConfirm={() => {
            const target = variantArchive
            setVariantArchive(null)
            void (async () => {
              setBusy(true)
              try {
                if (target.active) await archiveVariant(target.id)
                else await restoreVariant(target.id)
                if (editingId) await openEdit(editingId)
                await refresh()
              } catch (err) {
                setError(err)
              } finally {
                setBusy(false)
              }
            })()
          }}
          onCancel={() => setVariantArchive(null)}
        />
      ) : null}
    </section>
  )
}
