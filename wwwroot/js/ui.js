function toggleTheme() {
    const currentTheme = document.documentElement.getAttribute('data-theme');
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', newTheme);

    const themeToggle = document.querySelector('.theme-toggle');
    themeToggle.textContent = newTheme === 'dark' ? '☀️' : '🌙';
}
function showLoadingOverlay(show) {
    let overlay = document.getElementById('loadingOverlay');
    if (!overlay) {
        overlay = document.createElement('div');
        overlay.id = 'loadingOverlay';
        overlay.style.cssText = `
            position: fixed; top: 0; left: 0; width: 100vw; height: 100vh;
            background: rgba(255,255,255,0.5); z-index: 9999; display: flex;
            align-items: center; justify-content: center; font-size: 2em; color: #333; display: none;
        `;
        overlay.innerHTML = '<div>Refreshing...</div>';
        document.body.appendChild(overlay);
    }
    overlay.style.display = show ? 'flex' : 'none';
}

function initializeTheme() {
    document.documentElement.setAttribute('data-theme', 'light');
}

// Modal controls
function showAddDeviceModal() {
    document.getElementById('addDeviceModal').style.display = 'block';
}

function closeAddDeviceModal() {
    document.getElementById('addDeviceModal').style.display = 'none';
    document.getElementById('addDeviceForm').reset();
}

function showDiscoveryModal() {
    document.getElementById('discoveryModal').style.display = 'block';
    document.getElementById('discoveryLoading').style.display = 'block';
    document.getElementById('discoveryResults').style.display = 'none';
    scanNetwork();
}

function closeDiscoveryModal() {
    document.getElementById('discoveryModal').style.display = 'none';
}

function showUrlMonitorModal() {
    document.getElementById('urlMonitorModal').style.display = 'block';
}

function closeUrlMonitorModal() {
    document.getElementById('urlMonitorModal').style.display = 'none';
    document.getElementById('urlMonitorForm').reset();
}

// Close modals when clicking outside
window.onclick = function (event) {
    const modals = document.querySelectorAll('.modal');
    modals.forEach(modal => {
        if (event.target === modal) {
            modal.style.display = 'none';
        }
    });
}

function showNotification(message, type = 'info') {
    // Create notification element if it doesn't exist
    let notification = document.getElementById('notification');
    if (!notification) {
        notification = document.createElement('div');
        notification.id = 'notification';
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            padding: 15px 20px;
            border-radius: 8px;
            color: white;
            font-weight: bold;
            z-index: 9999;
            max-width: 400px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
            transform: translateX(100%);
            transition: transform 0.3s ease;
        `;
        document.body.appendChild(notification);
    }
}

    function showErrorState() {
        const container = document.getElementById('urlMonitorContainer');
        container.innerHTML = `
        <div class="error-message">
            <strong>Unable to connect to monitoring API</strong><br>
            Please check your backend connection and try again.
        </div>
    `;
    }
