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
function selectAllDevices(checked) {
    const checkboxes = document.querySelectorAll('.device-checkbox');
    checkboxes.forEach(cb => cb.checked = checked);
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

        loadMetrics();
        loadRecentDevices();
    } catch (error) {
        console.error('Error loading devices:', error);
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

function displayFilteredDevices(filteredDevices) {
    const container = document.getElementById('deviceListContainer');
    // Use same display logic as loadRecentDevices but with filtered data
    // Implementation similar to loadRecentDevices()
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


// Missing function: ImportData
function mapDeviceStatus(status) {
    switch (status) {
        case 1: return 'online';
        case 2: return 'offline';
        case 3: return 'warning';
        case 4: return 'error';
        default: return 'unknown';
    }
}
function exportData() {
    try {
        const response = await fetch('/api/export');
        const data = await response.json();

        const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }

