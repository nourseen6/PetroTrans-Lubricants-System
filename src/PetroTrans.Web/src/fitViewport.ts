const DESIGN_WIDTH = 1366
const DESIGN_HEIGHT = 768

export function applyViewportFit() {
  const width = window.visualViewport?.width ?? window.innerWidth
  const height = window.visualViewport?.height ?? window.innerHeight
  const scale = Math.min(width / DESIGN_WIDTH, height / DESIGN_HEIGHT, 1)
  document.documentElement.style.zoom = String(Math.max(0.4, scale))
}

export function startViewportFit() {
  applyViewportFit()
  window.addEventListener('resize', applyViewportFit)
  window.visualViewport?.addEventListener('resize', applyViewportFit)
}
