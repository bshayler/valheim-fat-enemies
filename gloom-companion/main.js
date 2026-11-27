const { app, BrowserWindow } = require('electron');
const path = require('path');
function create() {
  const win = new BrowserWindow({ width: 1280, height: 860, backgroundColor: '#1b1712', title: 'Gloom Companion' });
  win.loadFile(path.join(__dirname, 'index.html'));
}
app.whenReady().then(() => {
  create();
  app.on('activate', () => { if (!BrowserWindow.getAllWindows().length) create(); });
});
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
