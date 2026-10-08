type PrintMessage = { type: 'print' | 'pdf'; fileName?: string }

function host(): { postMessage: (msg: unknown) => void } | undefined {
  const chrome = (window as unknown as { chrome?: { webview?: { postMessage: (msg: unknown) => void } } }).chrome
  return chrome?.webview
}

export function requestPrint(title?: string) {
  if (title) document.title = title
  const bridge = host()
  if (bridge) {
    const message: PrintMessage = { type: 'print' }
    bridge.postMessage(message)
    return
  }
  window.print()
}

export function requestPdf(fileName: string) {
  const bridge = host()
  if (bridge) {
    const message: PrintMessage = { type: 'pdf', fileName }
    bridge.postMessage(message)
    return
  }
  window.print()
}
