let statusChart = null;
let responseChart = null;
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
