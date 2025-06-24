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
        monitorSsl: true
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
        monitor.status = 'checking';
        monitor.lastCheck = new Date().toISOString();
    });
    loadUrlMonitors();

    // Real API call would go here
    // checkAllUrlsAPI();
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

// Missing function: TestSimpleUrlAsync

// Missing function: CheckUrlAsync

function updateUrlMonitorStatus(monitorId, status, responseTime) {
    const monitor = urlMonitors.find(m => m.id === monitorId);
    if (monitor) {
        monitor.status = status;
        monitor.responseTime = responseTime;
        monitor.lastCheck = new Date().toISOString();
        loadMetrics();
        loadUrlMonitors();
    }
}

// Missing function: DisplayFilteredUrlMonitors

function filterUrlMonitors(searchTerm) {
    const filteredMonitors = urlMonitors.filter(monitor =>
        monitor.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        monitor.url.toLowerCase().includes(searchTerm.toLowerCase())
    );

    displayFilteredUrlMonitors(filteredMonitors);
}

// Missing function: UpdateMonitoringStats

