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
    loadMetrics();
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
function addUrlMonitor() {
    const form = document.getElementById('urlMonitorForm');
    const formData = new FormData(form);

    const monitor = {
        id: Date.now(),
        url: formData.get('url'),
        name: formData.get('name') || new URL(formData.get('url')).hostname,
        description: formData.get('description') || '',
        checkInterval: parseInt(formData.get('checkInterval')),
        timeout: parseInt(formData.get('timeout')),
        status: 'checking',
        responseTime: null,
        lastCheck: new Date().toISOString(),
        sslCert: null
    };

    urlMonitors.push(monitor);
    closeUrlMonitorModal();
    loadMetrics();
    loadUrlMonitors();

    // Real URL check would go here
    // checkUrl(monitor);
}

function checkAllUrls() {
    // Real implementation would check all URLs via API
    urlMonitors.forEach(monitor => {
        monitor.status = 'checking';
        monitor.lastCheck = new Date().toISOString();
    });
    loadUrlMonitors();

    // Real API call would go here
    // checkAllUrlsAPI();
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
    try {
        const response = await fetch('/api/urlmonitor/dashboard-data');
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const dashboardData = await response.json();

        // Update metrics using API data
        updateMetricsFromAPI(dashboardData);

        // Update URL monitors display
        updateUrlMonitorsFromAPI(dashboardData.Monitors || []);

        // Update charts
        updateCharts();

        return dashboardData;
    } catch (error) {
        console.error('Error loading dashboard data:', error);
        // Show error state
        showErrorState();
    }
}

function updateMetricsFromAPI(dashboardData) {
    const stats = dashboardData.Stats || {};

    // Update URL metrics from API
    const urlsUp = stats.UpMonitors || 0;
    const urlsDown = stats.DownMonitors || 0;

    // Certificate metrics from API
    const certsExpiring = dashboardData.ExpiringCertificates ? dashboardData.ExpiringCertificates.length : 0;
    const certsExpired = dashboardData.Monitors ? dashboardData.Monitors.filter(m =>
        m.Certificate && m.Certificate.DaysUntilExpiry <= 0
    ).length : 0;

    // Update metric cards
    document.querySelector('.metric-card.urls-up .metric-number').textContent = urlsUp;
    document.querySelector('.metric-card.urls-down .metric-number').textContent = urlsDown;
    document.querySelector('.metric-card.cert-expiring .metric-number').textContent = certsExpiring;
    document.querySelector('.metric-card.cert-expired .metric-number').textContent = certsExpired;
}

function updateUrlMonitorsFromAPI(monitors) {
    const container = document.getElementById('urlMonitorContainer');

    if (monitors.length === 0) {
        container.innerHTML = '<p style="text-align: center; color: var(--text-muted);">No URL monitors configured. Click "Add URL Monitor" to get started.</p>';
        return;
    }

    container.innerHTML = monitors.map(monitor => {
        const statusClass = `status-${monitor.Status.toLowerCase()}`;
        const statusText = monitor.Status.charAt(0).toUpperCase() + monitor.Status.slice(1);
        const lastCheck = new Date(monitor.LastCheck).toLocaleString();

        const certInfo = renderCertificateInfo(monitor);

        return `
            <div class="url-monitor-item ${statusClass}">
                <div class="url-info">
                    <strong>${monitor.Name}</strong>
                    <div class="url-details">
                        <span class="url">${monitor.Url}</span>
                        <span class="status ${statusClass}">${statusText}</span>
                        <span class="response-time">${monitor.ResponseTime ? `${monitor.ResponseTime}ms` : '—'}</span>
                    </div>
                </div>
                ${certInfo}
            </div>
        `;
    }).join('');
}

function renderCertificateInfo(monitor) {
    let certInfo = '';

    if (monitor.Certificate) {
        const cert = monitor.Certificate;
        const certClass = cert.DaysUntilExpiry <= 0 ? 'cert-expired' :
            cert.DaysUntilExpiry <= 30 ? 'cert-expiring' : 'cert-valid';

        const daysText = cert.DaysUntilExpiry <= 0 ? 'Expired' :
            cert.DaysUntilExpiry === 1 ? '1 day' :
                `${cert.DaysUntilExpiry} days`;

        const expiryDate = new Date(cert.ExpiryDate).toLocaleDateString();

        certInfo = `
            <div class="cert-info ${certClass}">
                <span class="cert-name">${cert.Subject || monitor.Name}</span>
                <span class="cert-expiry">Expires: ${expiryDate}</span>
                <span class="cert-days">${daysText}</span>
            </div>
        `;
    } else if (monitor.Url && monitor.Url.startsWith('https://')) {
        certInfo = '<div class="cert-info cert-none">No certificate info</div>';
    }

    return certInfo;
}

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
    const urlsUp = urlMonitors.filter(m => m.status === 'up').length;
    const urlsDown = urlMonitors.filter(m => ['down', 'error', 'timeout'].includes(m.status)).length;
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
        const statusClass = `status-${monitor.status}`;
        const statusText = monitor.status.charAt(0).toUpperCase() + monitor.status.slice(1);
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
            loadMetrics();
            loadRecentDevices();
            loadUrlMonitors();

            // Try to load real data from API
            loadDashboardData();

            // Set up auto-refresh for real data
            setInterval(() => {
                loadDashboardData();
            }, 30000); // Refresh every 30 seconds
        }

        // Start the application when page loads
        document.addEventListener('DOMContentLoaded', initialize);