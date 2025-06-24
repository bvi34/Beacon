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
