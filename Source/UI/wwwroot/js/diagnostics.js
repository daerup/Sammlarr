export function scrollToBottom(el) {
	if (!el) return;
	el.scrollTop = el.scrollHeight;
}

export async function downloadFile(url) {
	const response = await fetch(url);
	if (!response.ok) throw new Error(`Download failed: ${response.status}`);
	const blob = await response.blob();
	const disposition = response.headers.get('content-disposition') || '';
	const match = disposition.match(/filename="?([^";]+)"?/i);
	const filename = match ? match[1] : 'diagnostics.zip';
	const objectUrl = URL.createObjectURL(blob);
	const a = document.createElement('a');
	a.href = objectUrl;
	a.download = filename;
	document.body.appendChild(a);
	a.click();
	document.body.removeChild(a);
	URL.revokeObjectURL(objectUrl);
}
