let devices = [];
let urlMonitors = [];
let statusChart = null;
let responseChart = null;

// Theme toggle setup
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

// Device management
function addDevice() {
    const form = document.getElementById('addDeviceForm');
    const formData = new FormData(form);

    const device = {
        id: Date.now(),
        ipAddress: formData.get('ipAddress'),
        hostname: formData.get('hostname') || 'Unknown',
        deviceType: formData.get('deviceType') || 'other',
        description: formData.get('description') || '',
        monitoringEnabled: formData.get('monitoringEnabled') === 'on',
        status: 'checking',
        lastSeen: new Date().toISOString(),
        responseTime: null
    };

    devices.push(device);
    closeAddDeviceModal();
    updateMetricsFromApi(dashboardData)
    loadRecentDevices();

    // Real device ping would go here
    // pingDevice(device);
}

async function scanNetwork() {
    // Show loading state
    document.getElementById('discoveryLoading').style.display = 'block';
    document.getElementById('discoveryResults').style.display = 'none';

    try {
        // Call your API endpoint for local network discovery
        const response = await fetch('/api/discovery/scan-local', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const discoveryResult = await response.json();

        // Hide loading and show results
        document.getElementById('discoveryLoading').style.display = 'none';
        document.getElementById('discoveryResults').style.display = 'block';

        // Display the discovered devices
        displayDiscoveredDevices(discoveryResult);

    } catch (error) {
        console.error('Network discovery failed:', error);

        // Hide loading and show error
        document.getElementById('discoveryLoading').style.display = 'none';
        document.getElementById('discoveryResults').style.display = 'block';

        const container = document.getElementById('discoveredDevicesList');
        container.innerHTML = `
            <div style="text-align: center; color: var(--danger); padding: 20px;">
                <h4>Network Discovery Failed</h4>
                <p>Error: ${error.message}</p>
                <button onclick="scanNetwork()" class="btn btn-primary" style="margin-top: 10px;">
                    Try Again
                </button>
            </div>
        `;
    }
}

function displayDiscoveredDevices(discoveryResult) {
    const container = document.getElementById('discoveredDevicesList');

    if (discoveryResult.totalDevicesFound === 0) {
        container.innerHTML = `
            <div style="text-align: center; color: var(--text-muted); padding: 20px;">
                <h4>No Devices Found</h4>
                <p>No responsive devices were discovered on your network.</p>
            </div>
        `;
        return;
    }

    let html = `
        <div class="discovery-summary" style="margin-bottom: 20px; padding: 15px; background: var(--card-bg); border-radius: 8px;">
            <h4>Discovery Summary</h4>
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 15px; margin-top: 10px;">
                <div>
                    <strong>${discoveryResult.totalDevicesFound}</strong>
                    <div style="color: var(--text-muted); font-size: 0.9em;">Total Found</div>
                </div>
                <div>
                    <strong style="color: var(--success);">${discoveryResult.newDevices.length}</strong>
                    <div style="color: var(--text-muted); font-size: 0.9em;">New Devices</div>
                </div>
                <div>
                    <strong style="color: var(--warning);">${discoveryResult.existingDevices.length}</strong>
                    <div style="color: var(--text-muted); font-size: 0.9em;">Already Monitored</div>
                </div>
            </div>
        </div>
    `;

    // Display new devices first
    if (discoveryResult.newDevices.length > 0) {
        html += `
            <div class="device-section">
                <h5 style="color: var(--success); margin-bottom: 15px;">
                    <i class="fas fa-plus-circle"></i> New Devices (${discoveryResult.newDevices.length})
                </h5>
                <div class="device-grid">
                    ${discoveryResult.newDevices.map(device => createDeviceCard(device, true)).join('')}
                </div>
            </div>
        `;
    }

    // Display existing devices
    if (discoveryResult.existingDevices.length > 0) {
        html += `
            <div class="device-section" style="margin-top: 25px;">
                <h5 style="color: var(--warning); margin-bottom: 15px;">
                    <i class="fas fa-check-circle"></i> Already Monitored (${discoveryResult.existingDevices.length})
                </h5>
                <div class="device-grid">
                    ${discoveryResult.existingDevices.map(device => createDeviceCard(device, false)).join('')}
                </div>
            </div>
        `;
    }

    // Add action buttons for new devices
    if (discoveryResult.newDevices.length > 0) {
        html += `
            <div style="margin-top: 25px; text-align: center; padding: 20px; background: var(--card-bg); border-radius: 8px;">
                <button onclick="addAllNewDevices()" class="btn btn-success" style="margin-right: 10px;">
                    <i class="fas fa-plus"></i> Add All New Devices
                </button>
                <button onclick="addSelectedDevices()" class="btn btn-primary">
                    <i class="fas fa-check"></i> Add Selected Devices
                </button>
            </div>
        `;
    }

    container.innerHTML = html;
}

function createDeviceCard(device, isNew) {
    const statusColor = device.isReachable ? 'var(--success)' : 'var(--danger)';
    const deviceTypeIcon = getDeviceTypeIcon(device.deviceType);

    return `
        <div class="device-card" style="background: var(--card-bg); border-radius: 8px; padding: 15px; border: 1px solid var(--border-color);">
            ${isNew ? `<input type="checkbox" class="device-checkbox" data-ip="${device.ipAddress}" style="float: right;">` : ''}
            
            <div style="display: flex; align-items: center; margin-bottom: 10px;">
                <div style="font-size: 1.5em; margin-right: 10px;">${deviceTypeIcon}</div>
                <div>
                    <div style="font-weight: bold; color: var(--text-primary);">${device.hostname}</div>
                    <div style="color: var(--text-muted); font-size: 0.9em;">${device.ipAddress}</div>
                </div>
            </div>

            <div style="margin-bottom: 10px;">
                <span style="display: inline-block; padding: 2px 8px; border-radius: 12px; font-size: 0.8em; 
                           background: ${statusColor}20; color: ${statusColor};">
                    ${device.isReachable ? 'Online' : 'Offline'}
                </span>
                <span style="color: var(--text-muted); font-size: 0.9em; margin-left: 10px;">
                    ${device.responseTimeMs}ms
                </span>
            </div>

            <div style="color: var(--text-muted); font-size: 0.9em;">
                <div><strong>Type:</strong> ${device.deviceType}</div>
                ${device.openPorts && device.openPorts.length > 0 ?
            `<div><strong>Open Ports:</strong> ${device.openPorts.slice(0, 5).join(', ')}${device.openPorts.length > 5 ? '...' : ''}</div>` : ''}
                ${device.services && device.services.length > 0 ?
            `<div><strong>Services:</strong> ${device.services.slice(0, 3).map(s => s.serviceName).join(', ')}${device.services.length > 3 ? '...' : ''}</div>` : ''}
            </div>

            ${isNew ? `
                <button onclick="addSingleDevice('${device.ipAddress}')" class="btn btn-sm btn-primary" style="margin-top: 10px; width: 100%;">
                    <i class="fas fa-plus"></i> Add Device
                </button>
            ` : ''}
        </div>
    `;
}

function getDeviceTypeIcon(deviceType) {
    const icons = {
        'Router/Gateway': '🌐',
        'Network Switch': '🔄',
        'Server': '🖥️',
        'Web Server': '🌍',
        'Database Server': '🗄️',
        'Mail Server': '📧',
        'Printer': '🖨️',
        'Windows Computer': '💻',
        'Network Device': '📡',
        'Web Service': '🌐',
        'Unknown Device': '❓'
    };
    return icons[deviceType] || '📱';
}

// Function to add all new devices
async function addAllNewDevices() {
    const checkboxes = document.querySelectorAll('.device-checkbox');
    const ipAddresses = Array.from(checkboxes).map(cb => cb.dataset.ip);

    if (ipAddresses.length === 0) {
        showNotification('No new devices to add.', 'warning');
        return;
    }

    await addDevicesToMonitoring(ipAddresses);
}

// Function to add selected devices
async function addSelectedDevices() {
    const checkboxes = document.querySelectorAll('.device-checkbox:checked');
    const ipAddresses = Array.from(checkboxes).map(cb => cb.dataset.ip);

    if (ipAddresses.length === 0) {
        showNotification('Please select at least one device to add.', 'warning');
        return;
    }

    await addDevicesToMonitoring(ipAddresses);
}

// Function to add a single device
async function addSingleDevice(ipAddress) {
    await addDevicesToMonitoring([ipAddress]);
}

// Function to add devices to monitoring via API
async function addDevicesToMonitoring(ipAddresses) {
    try {
        // Show loading state
        showNotification('Adding devices to monitoring...', 'info');

        const response = await fetch('/api/discovery/add-devices', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                deviceIpAddresses: ipAddresses
            })
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const result = await response.json();

        // Show results
        let message = `Successfully added ${result.successfullyAdded} device(s) to monitoring.`;
        let type = 'success';

        if (result.failed > 0) {
            message += ` ${result.failed} device(s) failed to be added.`;
            type = result.successfullyAdded > 0 ? 'warning' : 'error';

            // Show detailed error information
            const failedDevices = result.results.filter(r => !r.success);
            if (failedDevices.length > 0) {
                console.error('Failed devices:', failedDevices);
                message += '\n\nFailed devices:\n' + failedDevices.map(d => `${d.ipAddress}: ${d.error}`).join('\n');
            }
        }

        showNotification(message, type);

        // Refresh the device list and metrics if any devices were added successfully
        if (result.successfullyAdded > 0) {
            await loadDashboardData();
            // Optionally refresh the scan to show updated status
            setTimeout(() => {
                scanNetwork();
            }, 1000);
        }

    } catch (error) {
        console.error('Failed to add devices:', error);
        showNotification(`Failed to add devices: ${error.message}`, 'error');
    }
}

// URL Monitor management
async function addUrlMonitor() {
    const form = document.getElementById('urlMonitorForm');
    const formData = new FormData(form);

    const monitor = {
        url: formData.get('url'),
        name: formData.get('name') || new URL(formData.get('url')).hostname,
        description: formData.get('description') || '',
        checkIntervalMinutes: parseInt(formData.get('checkInterval')),
        timeoutSeconds: parseInt(formData.get('timeout')),
        isActive: true,
        monitorSsl: true,
        UrlStatus: 'Unknown'
    };

    try {
        const response = await fetch('/api/monitoring/monitors', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(monitor)
        });
        if (!response.ok) throw new Error('Failed to add monitor');
        closeUrlMonitorModal();
        await loadDashboardData();
    } catch (e) {
        showNotification('Failed to add monitor: ' + e.message, 'error');
    }
}


function checkAllUrls() {
    // Real implementation would check all URLs via API
    urlMonitors.forEach(monitor => {
        monitor.urlStatus = 'checking';
        monitor.lastCheck = new Date().toISOString();
    });
    loadUrlMonitors();

    // Real API call would go here
    checkAllUrlsAPI();
}
async function checkAllUrlsAPI() {
    try {
        const response = await fetch('/api/monitoring/check-all', { method: 'POST' });
        console.log(response);
        if (!response.ok) throw new Error('Failed to check URLs');

        const results = await response.json();
        console.table(results);
        showNotification('All monitors checked successfully.', 'success');
        await loadDashboardData();
    } catch (e) {
        showNotification('Check failed: ' + e.message, 'error');
        console.error(e);
    }
}


// Notification system
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

    // Set notification style based on type
    const colors = {
        success: '#28a745',
        error: '#dc3545',
        warning: '#ffc107',
        info: '#17a2b8'
    };

    notification.style.backgroundColor = colors[type] || colors.info;
    notification.textContent = message;

    // Show notification
    notification.style.transform = 'translateX(0)';

    // Hide after 5 seconds
    setTimeout(() => {
        notification.style.transform = 'translateX(100%)';
    }, 5000);
}

// API integration functions
async function loadDashboardData() {
    showLoadingOverlay(true); // Show overlay while loading
    console.log("Dashboard being loaded");
    try {
        const response = await fetch('/api/monitoring/dashboard-data');
        if (!response.ok) {
            throw new Error(`HTTP error! Status: ${response.status}`);
        }

        const dashboardData = await response.json();

        // Update metrics, monitors, and charts
        updateMetricsFromApi(dashboardData);
        updateUrlMonitorsFromApi(dashboardData.monitors || []);
        updateCharts();

        return dashboardData;
    } catch (error) {
        console.error('Error loading dashboard data:', error);
        // Optionally show error UI
    } finally {
        showLoadingOverlay(false); // Hide overlay after loading
    }
}

function updateMetricsFromApi(dashboardData) {
    const stats = dashboardData.stats || {};
    const totalDevices = stats.totalDevices || 0;      // 8
    const onlineDevices = stats.onlineDevices || 0;    // 7
    const offlineDevices = stats.offlineDevices || 0;  // 1
    const urlsUp = stats.upMonitors || 0;
    const urlsDown = stats.downMonitors || 0;

    const certsExpiring = dashboardData.expiringCertificates
        ? dashboardData.expiringCertificates.length
        : 0;
    const certsExpired = dashboardData.monitors
        ? dashboardData.monitors.filter(m =>
            m.certificate && m.certificate.daysUntilExpiry <= 0
        ).length
        : 0;

    // Fix the assignments:
    document.querySelector('.metric-card.urls-up .metric-number').textContent = urlsUp;
    document.querySelector('.metric-card.urls-down .metric-number').textContent = urlsDown;
    document.querySelector('.metric-card.cert-expiring .metric-number').textContent = certsExpiring;
    document.querySelector('.metric-card.cert-expired .metric-number').textContent = certsExpired;

    // Fix these assignments:
    document.querySelector('.metric-card.total .metric-number').textContent = totalDevices;    // Total Devices = 8
    document.querySelector('.metric-card.warning .metric-number').textContent = onlineDevices; // Devices Online = 7
    document.querySelector('.metric-card.offline .metric-number').textContent = offlineDevices; // Devices Offline = 1

    // What should "Recently Added" show? If it's supposed to show recently added devices, 
    // you need that data from your API. For now, I'll assume it should be 0:
    // document.querySelector('.metric-card.recently-added .metric-number').textContent = 0;

    firstLoad = false;
}

function updateUrlMonitorsFromApi(monitors) {
    const container = document.getElementById('urlMonitorContainer');

    if (!monitors || monitors.length === 0) {
        container.innerHTML = `
            <p style="text-align: center; color: var(--text-muted);">
                No URL monitors configured. Click "Add URL Monitor" to get started.
            </p>
        `;
        return;
    }
    console.log('Sample monitor:', monitors[0]);

    container.innerHTML = monitors.map(monitor => {
        const statusClass = `status-${monitor.urlStatus.toLowerCase()}`;
        const statusText = monitor.urlStatus.charAt(0).toUpperCase() + monitor.urlStatus.slice(1);
        const lastCheck = new Date(monitor.lastCheck).toLocaleString();
        const certInfo = renderCertificateInfo(monitor);

        return `
            <div class="url-monitor-item ${statusClass}">
                <div class="url-info">
                    <strong>${monitor.name}</strong>
                    <div class="url-details">
                        <span class="url">${monitor.url}</span>
                        <span class="status ${statusClass}">${statusText}</span>
                        <span class="response-time">${monitor.responseTime ? `${monitor.responseTime}ms` : '—'}</span>
                    </div>
                </div>
                ${certInfo}
            </div>
        `;
    }).join('');
}

function renderCertificateInfo(monitor) {
    if (!monitor.certificate) {
        return '<div class="cert-info cert-none">No certificate info</div>';
    }

    if (!monitor.certificate) return '';

    const cert = monitor.certificate;
    const certClass =
        cert.daysUntilExpiry <= 0 ? 'cert-expired' :
            cert.daysUntilExpiry <= 30 ? 'cert-expiring' : 'cert-valid';

    const daysText =
        cert.daysUntilExpiry < 0 ? `Expired ${Math.abs(cert.daysUntilExpiry)} days ago` :
            cert.daysUntilExpiry === 0 ? 'Expires today' :
                cert.daysUntilExpiry === 1 ? '1 day' :
                    `${cert.daysUntilExpiry} days`;
    console.log('Days Left:', daysText);

    const expiryDate = new Date(cert.expiryDate).toLocaleDateString();

    return `
        <div class="cert-info ${certClass}">
            <span class="cert-name">${cert.subject || monitor.name}</span>
            <span class="cert-expiry">Expires: ${expiryDate}</span>
            <span class="cert-days">${daysText}</span>
        </div>
    `;
}

let firstLoad = true;


// Data loading functions
function loadMetrics() {
    // Device metrics (local data)
    const onlineDevices = devices.filter(d => d.status === 'online').length;
    const offlineDevices = devices.filter(d => d.status === 'offline').length;
    const recentDevices = devices.filter(d => {
        const deviceTime = new Date(d.lastSeen);
        const hourAgo = new Date(Date.now() - 60 * 60 * 1000);
        return deviceTime > hourAgo;
    }).length;

    // URL metrics (local data - will be overridden by API)
    const urlsUp = urlMonitors.filter(m => m.urlStatus === 'up').length;
    const urlsDown = urlMonitors.filter(m => ['down', 'error', 'timeout'].includes(m.urlStatus)).length;
    const certsExpiring = urlMonitors.filter(m =>
        m.sslCert && m.sslCert.daysUntilExpiry <= 30 && m.sslCert.daysUntilExpiry > 0
    ).length;
    const certsExpired = urlMonitors.filter(m =>
        m.sslCert && m.sslCert.daysUntilExpiry <= 0
    ).length;

    // Update all metric cards
    document.querySelector('.metric-card.online .metric-number').textContent = onlineDevices;
    document.querySelector('.metric-card.offline .metric-number').textContent = offlineDevices;
    document.querySelector('.metric-card.warning .metric-number').textContent = recentDevices;
    document.querySelector('.metric-card.total .metric-number').textContent = devices.length;
    document.querySelector('.metric-card.urls-up .metric-number').textContent = urlsUp;
    document.querySelector('.metric-card.urls-down .metric-number').textContent = urlsDown;
    document.querySelector('.metric-card.cert-expiring .metric-number').textContent = certsExpiring;
    document.querySelector('.metric-card.cert-expired .metric-number').textContent = certsExpired;

    updateCharts();
}

function loadRecentDevices() {
    const container = document.getElementById('deviceListContainer');

    if (devices.length === 0) {
        container.innerHTML = '<p style="text-align: center; color: var(--text-muted);">No devices added yet. Click "Add Device" to get started.</p>';
        return;
    }

    const sortedDevices = [...devices].sort((a, b) => new Date(b.lastSeen) - new Date(a.lastSeen));

    container.innerHTML = sortedDevices.map(device => {
        const statusIcon = device.status === 'online' ? '🟢' :
            device.status === 'offline' ? '🔴' : '🟡';
        const lastSeen = new Date(device.lastSeen).toLocaleString();

        return `
            <div class="device-item">
                <a href="#" class="device-link">
                    <div>
                        <strong>${statusIcon} ${device.hostname}</strong>
                        <div class="ip">${device.ipAddress} • ${device.deviceType} • Last seen: ${lastSeen}</div>
                    </div>
                    </div>
                    <div style="text-align: right;">
                        ${device.responseTime ? `${device.responseTime}ms` : '—'}
                    </div>
                </a>
            </div>
        `;
    }).join('');
}

function loadUrlMonitors() {
    const container = document.getElementById('urlMonitorContainer');

    if (urlMonitors.length === 0) {
        container.innerHTML = '<p style="text-align: center; color: var(--text-muted);">No URL monitors configured. Click "Add URL Monitor" to get started.</p>';
        return;
    }

    container.innerHTML = urlMonitors.map(monitor => {
        const statusClass = `status-${monitor.urlStatus}`;
        const statusText = monitor.urlStatus.charAt(0).toUpperCase() + monitor.urlStatus.slice(1);
        const lastCheck = new Date(monitor.lastCheck).toLocaleString();

        let certInfo = '';
        if (monitor.sslCert) {
            const certClass = monitor.sslCert.daysUntilExpiry <= 0 ? 'cert-expired' :
                monitor.sslCert.daysUntilExpiry <= 30 ? 'cert-expiring' : 'cert-valid';
            const daysText = monitor.sslCert.daysUntilExpiry <= 0 ? 'Expired' :
                `${monitor.sslCert.daysUntilExpiry} days`;

            certInfo = `
                <div class="cert-info ${certClass}">
                    <span class="cert-name">${monitor.sslCert.subject}</span>
                    <span class="cert-expiry">Expires: ${new Date(monitor.sslCert.expiryDate).toLocaleDateString()}</span>
                    <span class="cert-days">${daysText}</span>
                </div>
            `;
        } else if (monitor.url.startsWith('https://')) {
            certInfo = '<div class="cert-info cert-none">No certificate info</div>';
        }

        return `
            <div class="url-monitor-item ${statusClass}">
                <div class="url-info">
                    <strong>${monitor.name}</strong>
                    <div class="url-details">
                        <span class="url">${monitor.url}</span>
                        <span class="status ${statusClass}">${statusText}</span>
                        <span class="response-time">${monitor.responseTime ? `${monitor.responseTime}ms` : '—'}</span>
                    </div>
                </div>
                ${certInfo}
            </div>
        `;
    }).join('');
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

// Chart functions
function updateCharts() {
    updateStatusChart();
    updateResponseChart();
}

function updateStatusChart() {
    const ctx = document.getElementById('statusChart').getContext('2d');

    if (statusChart) {
        statusChart.destroy();
    }

    // Real implementation would use historical data from API
    const hours = Array.from({ length: 24 }, (_, i) => `${i}:00`);
    const onlineData = Array.from({ length: 24 }, () => 0);
    const offlineData = Array.from({ length: 24 }, () => 0);

    statusChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: hours,
            datasets: [{
                label: 'Online',
                data: onlineData,
                borderColor: '#28a745',
                backgroundColor: 'rgba(40, 167, 69, 0.1)',
                tension: 0.4
            }, {
                label: 'Offline',
                data: offlineData,
                borderColor: '#dc3545',
                backgroundColor: 'rgba(220, 53, 69, 0.1)',
                tension: 0.4
            }]
        },
        options: {
            responsive: true,
            plugins: {
                legend: {
                    position: 'top',
                }
            },
            scales: {
                y: {
                    beginAtZero: true
                }
            }
        }
    });
}

function updateResponseChart() {
    const ctx = document.getElementById('responseChart').getContext('2d');

    if (responseChart) {
        responseChart.destroy();
    }

    const urls = urlMonitors.slice(0, 6).map(m => m.name);
    const responseTimes = urlMonitors.slice(0, 6).map(m => m.responseTime || 0);

    responseChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: urls,
            datasets: [{
                label: 'Response Time (ms)',
                data: responseTimes,
                backgroundColor: [
                    '#667eea',
                    '#764ba2',
                    '#f093fb',
                    '#28a745',
                    '#17a2b8',
                    '#ffc107'
                ]
            }]
        },
        options: {
            responsive: true,
            plugins: {
                legend: {
                    display: false
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Response Time (ms)'
                    }
                }
            }
        }
    });
}

// Certificate management functions
async function loadExpiringCertificates(days = 30) {
    try {
        const response = await fetch(`/api/urlmonitor/certificates/expiring?days=${days}`);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const expiringCerts = await response.json();
        return expiringCerts;
    } catch (error) {
        console.error('Error loading expiring certificates:', error);
        return [];
    }
}

// Load devices from the backend API and refresh metrics/display
async function loadDevicesFromAPI() {
    try {
        const response = await fetch('/devices/api/devices');
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();

        // Map API devices to local format expected by the UI
        devices = data.map(d => ({
            id: d.id,
            ipAddress: d.ipAddress,
            hostname: d.hostname,
            deviceType: d.deviceType || '',
            description: d.description || '',
            status: mapDeviceStatus(d.status),
            lastSeen: d.lastSeen,
            responseTime: null
        }));

        updateMetricsFromApi(dashboardData)
        loadRecentDevices();
    } catch (error) {
        console.error('Error loading devices:', error);
    }
}

// Helper to convert enum values returned by the API to status strings
function mapDeviceStatus(status) {
    switch (status) {
        case 1: return 'online';
        case 2: return 'offline';
        case 3: return 'warning';
        case 4: return 'error';
        default: return 'unknown';
    }
}
async function loadAllCertificates() {
    try {
        const response = await fetch('/api/urlmonitor/certificates');
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const certificates = await response.json();
        return certificates;
    } catch (error) {
        console.error('Error loading certificates:', error);
        return [];
    }
}

// Initialize the application
function initialize() {
    initializeTheme();
    // Remove this line: updateMetricsFromApi(dashboardData)
    loadRecentDevices();
    loadUrlMonitors();
    // Show overlay on first load
    showLoadingOverlay(true);
    // Try to load real data from API
    loadDashboardData().finally(() => {
        showLoadingOverlay(false);
    });
    // Set up auto-refresh for real data
    setInterval(() => {
        loadDashboardData();
    }, 30000); // Refresh every 30 seconds
}
// 5. CERTIFICATE MANAGEMENT
// ============================================================================

async function loadCertificatesList() {
    try {
        const certificates = await loadAllCertificates();
        displayCertificatesList(certificates);
    } catch (error) {
        showNotification('Error loading certificates: ' + error.message, 'error');
    }
}

function displayCertificatesList(certificates) {
    const container = document.getElementById('certificatesList');

    if (certificates.length === 0) {
        container.innerHTML = '<p>No SSL certificates found.</p>';
        return;
    }

    container.innerHTML = certificates.map(cert => {
        const daysUntilExpiry = Math.ceil((new Date(cert.expiryDate) - new Date()) / (1000 * 60 * 60 * 24));
        const statusClass = daysUntilExpiry <= 0 ? 'expired' :
            daysUntilExpiry <= 30 ? 'expiring' : 'valid';

        return `
            <div class="certificate-item ${statusClass}">
                <div class="cert-info">
                    <strong>${cert.subject}</strong>
                    <div>Domain: ${cert.domain}</div>
                    <div>Issuer: ${cert.issuer}</div>
                    <div>Expires: ${new Date(cert.expiryDate).toLocaleDateString()}</div>
                </div>
                <div class="cert-status">
                    <span class="days-remaining">${daysUntilExpiry <= 0 ? 'Expired' : `${daysUntilExpiry} days`}</span>
                </div>
            </div>
        `;
    }).join('');
}

// 6. EXPORT/IMPORT FUNCTIONS
// ============================================================================

async function exportData() {
    try {
        const response = await fetch('/api/export');
        const data = await response.json();

        const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `monitoring-data-${new Date().toISOString().split('T')[0]}.json`;
        a.click();
        URL.revokeObjectURL(url);

        showNotification('Data exported successfully', 'success');
    } catch (error) {
        showNotification('Error exporting data: ' + error.message, 'error');
    }
}

function importData() {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';

    input.onchange = async (event) => {
        const file = event.target.files[0];
        if (!file) return;

        try {
            const text = await file.text();
            const data = JSON.parse(text);

            const response = await fetch('/api/import', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data)
            });

            if (response.ok) {
                showNotification('Data imported successfully', 'success');
                loadDashboardData();
                loadDevicesFromAPI();
            } else {
                throw new Error('Failed to import data');
            }
        } catch (error) {
            showNotification('Error importing data: ' + error.message, 'error');
        }
    };

    input.click();
}

// 7. SEARCH AND FILTER FUNCTIONS
// ============================================================================

function filterDevices(searchTerm) {
    const filteredDevices = devices.filter(device =>
        device.hostname.toLowerCase().includes(searchTerm.toLowerCase()) ||
        device.ipAddress.includes(searchTerm) ||
        device.deviceType.toLowerCase().includes(searchTerm.toLowerCase())
    );

    displayFilteredDevices(filteredDevices);
}

function filterUrlMonitors(searchTerm) {
    const filteredMonitors = urlMonitors.filter(monitor =>
        monitor.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        monitor.url.toLowerCase().includes(searchTerm.toLowerCase())
    );

    displayFilteredUrlMonitors(filteredMonitors);
}

function displayFilteredDevices(filteredDevices) {
    const container = document.getElementById('deviceListContainer');
    // Use same display logic as loadRecentDevices but with filtered data
    // Implementation similar to loadRecentDevices()
}

function displayFilteredUrlMonitors(filteredMonitors) {
    const container = document.getElementById('urlMonitorContainer');
    // Use same display logic as loadUrlMonitors but with filtered data
    // Implementation similar to loadUrlMonitors()
}

// 8. BULK OPERATIONS
// ============================================================================

function selectAllDevices(checked) {
    const checkboxes = document.querySelectorAll('.device-checkbox');
    checkboxes.forEach(cb => cb.checked = checked);
}

async function bulkDeleteDevices() {
    const checkedBoxes = document.querySelectorAll('.device-checkbox:checked');
    const deviceIds = Array.from(checkedBoxes).map(cb => cb.dataset.deviceId);

    if (deviceIds.length === 0) {
        showNotification('No devices selected', 'warning');
        return;
    }

    if (!confirm(`Delete ${deviceIds.length} selected devices?`)) return;

    try {
        const response = await fetch('/api/devices/bulk-delete', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ deviceIds })
        });

        if (response.ok) {
            showNotification(`${deviceIds.length} devices deleted successfully`, 'success');
            loadDevicesFromAPI();
        } else {
            throw new Error('Failed to delete devices');
        }
    } catch (error) {
        showNotification('Error deleting devices: ' + error.message, 'error');
    }
}

// 9. REAL-TIME UPDATES (WebSocket)
// ============================================================================

function initializeWebSocket() {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${protocol}//${window.location.host}/ws`;

    const ws = new WebSocket(wsUrl);

    ws.onmessage = (event) => {
        const data = JSON.parse(event.data);
        handleRealTimeUpdate(data);
    };

    ws.onclose = () => {
        // Reconnect after 5 seconds
        setTimeout(initializeWebSocket, 5000);
    };

    ws.onerror = (error) => {
        console.error('WebSocket error:', error);
    };
}

function handleRealTimeUpdate(data) {
    switch (data.type) {
        case 'device_status':
            updateDeviceStatus(data.deviceId, data.status, data.responseTime);
            break;
        case 'url_status':
            updateUrlMonitorStatus(data.monitorId, data.status, data.responseTime);
            break;
        case 'alert':
            showNotification(data.message, data.level);
            break;
    }
}

function updateDeviceStatus(deviceId, status, responseTime) {
    const device = devices.find(d => d.id === deviceId);
    if (device) {
        device.status = status;
        device.responseTime = responseTime;
        device.lastSeen = new Date().toISOString();
        updateMetricsFromApi(dashboardData)
        loadRecentDevices();
    }
}

function updateUrlMonitorStatus(monitorId, status, responseTime) {
    const monitor = urlMonitors.find(m => m.id === monitorId);
    if (monitor) {
        monitor.urlStatus = status;
        monitor.responseTime = responseTime;
        monitor.lastCheck = new Date().toISOString();
        updateMetricsFromApi(dashboardData)
        loadUrlMonitors();
    }
}

// 10. UTILITY FUNCTIONS
// ============================================================================

function formatUptime(seconds) {
    const days = Math.floor(seconds / 86400);
    const hours = Math.floor((seconds % 86400) / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);

    if (days > 0) return `${days}d ${hours}h ${minutes}m`;
    if (hours > 0) return `${hours}h ${minutes}m`;
    return `${minutes}m`;
}

function formatBytes(bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

function isValidIP(ip) {
    const regex = /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$/;
    return regex.test(ip);
}

function isValidUrl(url) {
    try {
        new URL(url);
        return true;
    }
    catch (error) {
        return false;
    }
}

// Initialize WebSocket connection when app starts
document.addEventListener('DOMContentLoaded', () => {
    initialize();
    initializeWebSocket();
});
// Start the application when page loads
document.addEventListener('DOMContentLoaded', initialize);