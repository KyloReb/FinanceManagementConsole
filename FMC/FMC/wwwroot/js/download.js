window.fmc_downloadFile = async (fileName, contentType, contentStreamReference) => {
  const arrayBuffer = await contentStreamReference.arrayBuffer();
  const blob = new Blob([arrayBuffer], { type: contentType });
  const url = URL.createObjectURL(blob);
  const anchorElement = document.createElement('a');
  anchorElement.href = url;
  anchorElement.download = fileName ?? '';
  anchorElement.click();
  anchorElement.remove();
  URL.revokeObjectURL(url);
}

window.downloadFileFromStream = window.fmc_downloadFile;

/**
 * fmcDownloadBase64
 * Triggers a browser file download from a base64-encoded content string.
 * Used by Blazor components (e.g. AuditLogDetailDialog) to export data
 * client-side without a server round-trip.
 *
 * @param {string} fileName     - The filename for the downloaded file.
 * @param {string} contentType  - The MIME type (e.g. "text/plain").
 * @param {string} base64       - The base64-encoded file content.
 */
window.fmcDownloadBase64 = (fileName, contentType, base64) => {
  const byteChars = atob(base64);
  const byteNums = new Array(byteChars.length);
  for (let i = 0; i < byteChars.length; i++) {
    byteNums[i] = byteChars.charCodeAt(i);
  }
  const byteArray = new Uint8Array(byteNums);
  const blob = new Blob([byteArray], { type: contentType });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName ?? 'download';
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
};
