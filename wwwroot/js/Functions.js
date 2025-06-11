
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

        function scanNetwork() {
            // Real network discovery would go here
            // This would scan the actual network for devices
            setTimeout(() => {
                document.getElementById('discoveryLoading').style.display = 'none';
                document.getElementById('discoveryResults').style.display = 'block';

                const container = document.getElementById('discoveredDevicesList');
                container.innerHTML = '<p style="text-align: center; color: var(--text-muted);">Network scanning requires backend API integration</p>';
            }, 2000);
        }

        function addSelectedDevices() {
            // Real implementation would add selected discovered devices
            closeDiscoveryModal();
            alert('Device addition requires backend API integration');
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