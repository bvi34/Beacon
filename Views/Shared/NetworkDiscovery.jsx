import React, { useState, useEffect } from 'react';
import { Search, Wifi, Server, Shield, Plus, Check, X, Loader2, Network, Globe } from 'lucide-react';

const NetworkDiscovery = () => {
    const [isScanning, setIsScanning] = useState(false);
    const [discoveredDevices, setDiscoveredDevices] = useState([]);
    const [selectedDevices, setSelectedDevices] = useState(new Set());
    const [scanType, setScanType] = useState('local');
    const [networkRange, setNetworkRange] = useState('');
    const [lastScanTime, setLastScanTime] = useState(null);
    const [discoveryStats, setDiscoveryStats] = useState(null);
    const [isAddingDevices, setIsAddingDevices] = useState(false);

    // Load discovery status on component mount
    useEffect(() => {
        loadDiscoveryStatus();
    }, []);

    const loadDiscoveryStatus = async () => {
        try {
            const response = await fetch('/api/discovery/status');
            if (response.ok) {
                const status = await response.json();
                setDiscoveryStats(status);
            }
        } catch (error) {
            console.error('Failed to load discovery status:', error);
        }
    };

    const startDiscovery = async () => {
        setIsScanning(true);
        setDiscoveredDevices([]);
        setSelectedDevices(new Set());

        try {
            const endpoint = scanType === 'local' ? '/api/discovery/scan-local' : '/api/discovery/scan-range';
            const body = scanType === 'local' ? {} : { networkRange };

            const response = await fetch(endpoint, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(body),
            });

            if (response.ok) {
                const result = await response.json();
                setDiscoveredDevices([...result.newDevices, ...result.existingDevices]);
                setLastScanTime(new Date(result.scanCompletedAt));

                // Auto-select new devices
                const newDeviceIps = new Set(result.newDevices.map(d => d.ipAddress));
                setSelectedDevices(newDeviceIps);
            } else {
                const error = await response.json();
                alert(`Discovery failed: ${error.error}`);
            }
        } catch (error) {
            console.error('Discovery error:', error);
            alert('Network discovery failed. Please try again.');
        } finally {
            setIsScanning(false);
        }
    };

    const addSelectedDevices = async () => {
        if (selectedDevices.size === 0) {
            alert('Please select at least one device to add.');
            return;
        }

        setIsAddingDevices(true);

        try {
            const response = await fetch('/api/discovery/add-devices', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    deviceIpAddresses: Array.from(selectedDevices),
                }),
            });

            if (response.ok) {
                const result = await response.json();
                alert(`Successfully added ${result.successfullyAdded} of ${result.totalRequested} devices.`);

                // Remove successfully added devices from the discovered list
                const successfulIps = new Set(
                    result.results.filter(r => r.success).map(r => r.ipAddress)
                );

                setDiscoveredDevices(prev =>
                    prev.map(device => ({
                        ...device,
                        alreadyExists: successfulIps.has(device.ipAddress) || device.alreadyExists
                    }))
                );

                setSelectedDevices(new Set());
                loadDiscoveryStatus(); // Refresh stats
            } else {
                const error = await response.json();
                alert(`Failed to add devices: ${error.error}`);
            }
        } catch (error) {
            console.error('Add devices error:', error);
            alert('Failed to add devices. Please try again.');
        } finally {
            setIsAddingDevices(false);
        }
    };

    const toggleDeviceSelection = (ipAddress) => {
        const newSelection = new Set(selectedDevices);
        if (newSelection.has(ipAddress)) {
            newSelection.delete(ipAddress);
        } else {
            newSelection.add(ipAddress);
        }
        setSelectedDevices(newSelection);
    };

    const selectAllNewDevices = () => {
        const newDevices = discoveredDevices.filter(d => !d.alreadyExists);
        const newDeviceIps = new Set(newDevices.map(d => d.ipAddress));
        setSelectedDevices(newDeviceIps);
    };
    const selectAllDevices = () => {
        const selectable = discoveredDevices;
        const allIps = new Set(selectable.map(d));
        setSelectedDevices(allIps);
    }

    const getDeviceIcon = (deviceType) => {
        switch (deviceType.toLowerCase()) {
            case 'server':
            case 'web server':
            case 'database server':
            case 'mail server':
                return <Server className="w-5 h-5" />;
            case 'router/gateway':
            case 'network switch':
            case 'network device':
                return <Network className="w-5 h-5" />;
            default:
                return <Globe className="w-5 h-5" />;
        }
    };

    const getStatusBadge = (device) => {
        if (device.alreadyExists) {
            return (
                <span className="inline-flex items-center px-2 py-1 text-xs font-medium bg-blue-100 text-blue-800 rounded-full">
                    <Check className="w-3 h-3 mr-1" />
                    Managed
                </span>
            );
        }
        return (
            <span className="inline-flex items-center px-2 py-1 text-xs font-medium bg-green-100 text-green-800 rounded-full">
                <Plus className="w-3 h-3 mr-1" />
                New
            </span>
        );
    };

    return (
        <div className="max-w-7xl mx-auto p-6 space-y-6">
            {/* Header */}
            <div className="bg-white rounded-lg shadow p-6">
                <h1 className="text-2xl font-bold text-gray-900 mb-2">Network Discovery</h1>
                <p className="text-gray-600">Discover and add network devices to monitoring</p>
            </div>

            {/* Discovery Stats */}
            {discoveryStats && (
                <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                    <div className="bg-white rounded-lg shadow p-4">
                        <div className="flex items-center">
                            <Server className="w-8 h-8 text-blue-500" />
                            <div className="ml-4">
                                <p className="text-sm font-medium text-gray-600">Total Devices</p>
                                <p className="text-2xl font-bold text-gray-900">{discoveryStats.totalManagedDevices}</p>
                            </div>
                        </div>
                    </div>
                    <div className="bg-white rounded-lg shadow p-4">
                        <div className="flex items-center">
                            <div className="w-8 h-8 bg-green-100 rounded-full flex items-center justify-center">
                                <div className="w-3 h-3 bg-green-500 rounded-full"></div>
                            </div>
                            <div className="ml-4">
                                <p className="text-sm font-medium text-gray-600">Online</p>
                                <p className="text-2xl font-bold text-green-600">{discoveryStats.onlineDevices}</p>
                            </div>
                        </div>
                    </div>
                    <div className="bg-white rounded-lg shadow p-4">
                        <div className="flex items-center">
                            <div className="w-8 h-8 bg-red-100 rounded-full flex items-center justify-center">
                                <div className="w-3 h-3 bg-red-500 rounded-full"></div>
                            </div>
                            <div className="ml-4">
                                <p className="text-sm font-medium text-gray-600">Offline</p>
                                <p className="text-2xl font-bold text-red-600">{discoveryStats.offlineDevices}</p>
                            </div>
                        </div>
                    </div>
                    <div className="bg-white rounded-lg shadow p-4">
                        <div className="flex items-center">
                            <Plus className="w-8 h-8 text-purple-500" />
                            <div className="ml-4">
                                <p className="text-sm font-medium text-gray-600">Recent</p>
                                <p className="text-2xl font-bold text-purple-600">{discoveryStats.recentlyAddedDevices}</p>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* Discovery Controls */}
            <div className="bg-white rounded-lg shadow p-6">
                <div className="space-y-4">
                    <div className="flex flex-col sm:flex-row gap-4">
                        <div className="flex-1">
                            <label className="block text-sm font-medium text-gray-700 mb-2">
                                Scan Type
                            </label>
                            <select
                                value={scanType}
                                onChange={(e) => setScanType(e.target.value)}
                                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                                disabled={isScanning}
                            >
                                <option value="local">Local Network</option>
                                <option value="range">Custom Range</option>
                            </select>
                        </div>

                        {scanType === 'range' && (
                            <div className="flex-1">
                                <label className="block text-sm font-medium text-gray-700 mb-2">
                                    Network Range
                                </label>
                                <input
                                    type="text"
                                    value={networkRange}
                                    onChange={(e) => setNetworkRange(e.target.value)}
                                    placeholder="192.168.1.1-254 or 10.0.0.0/24"
                                    className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                                    disabled={isScanning}
                                />
                            </div>
                        )}
                    </div>

                    <div className="flex gap-3">
                        <button
                            onClick={startDiscovery}
                            disabled={isScanning || (scanType === 'range' && !networkRange.trim())}
                            className="inline-flex items-center px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            {isScanning ? (
                                <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                            ) : (
                                <Search className="w-4 h-4 mr-2" />
                            )}
                            {isScanning ? 'Scanning...' : 'Start Discovery'}
                        </button>

                        {discoveredDevices.length > 0 && selectedDevices.size > 0 && (
                            <button
                                onClick={addSelectedDevices}
                                disabled={isAddingDevices}
                                className="inline-flex items-center px-4 py-2 bg-green-600 text-white rounded-md hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-green-500 disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                {isAddingDevices ? (
                                    <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                                ) : (
                                    <Plus className="w-4 h-4 mr-2" />
                                )}
                                Add Selected ({selectedDevices.size})
                            </button>
                        )}
                    </div>

                    {lastScanTime && (
                        <p className="text-sm text-gray-500">
                            Last scan completed: {lastScanTime.toLocaleString()}
                        </p>
                    )}
                </div>
            </div>

            {/* Discovery Results */}
            {discoveredDevices.length > 0 && (
                <div className="bg-white rounded-lg shadow">
                    <div className="px-6 py-4 border-b border-gray-200">
                        <div className="flex items-center justify-between">
                            <h2 className="text-lg font-medium text-gray-900">
                                Discovered Devices ({discoveredDevices.length})
                            </h2>
                            <button
                                onClick={selectAllDevices}
                                className="text-sm text-blue-600 hover:text-blue-800"
                            >
                                Select All
                            </button>
                                )}
                            {discoveredDevices.some(d => !d.alreadyExists) && (
                                <button
                                    onClick={selectAllNewDevices}
                                    className="text-sm text-blue-600 hover:text-blue-800"
                                >
                                    Select All New Devices
                                </button>
                            )}
                        </div>
                    </div>

                    <div className="divide-y divide-gray-200">
                        {discoveredDevices.map((device) => (
                            <div key={device.ipAddress} className="p-6 hover:bg-gray-50">
                                <div className="flex items-center justify-between">
                                    <div className="flex items-center space-x-4">
                                        <input
                                            type="checkbox"
                                            checked={selectedDevices.has(device.ipAddress)}
                                            onChange={() => toggleDeviceSelection(device.ipAddress)}
                                            disabled={device.alreadyExists}
                                            className="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded disabled:opacity-50"
                                        />

                                        <div className="flex items-center space-x-3">
                                            {getDeviceIcon(device.deviceType)}
                                            <div>
                                                <h3 className="text-sm font-medium text-gray-900">
                                                    {device.hostname}
                                                </h3>
                                                <p className="text-sm text-gray-500">{device.ipAddress}</p>
                                            </div>
                                        </div>
                                    </div>

                                    <div className="flex items-center space-x-4">
                                        <div className="text-right">
                                            <p className="text-sm font-medium text-gray-900">{device.deviceType}</p>
                                            <p className="text-sm text-gray-500">{device.responseTime}ms</p>
                                        </div>

                                        {device.openPorts && device.openPorts.length > 0 && (
                                            <div className="text-right">
                                                <p className="text-sm font-medium text-gray-900">
                                                    {device.openPorts.length} ports
                                                </p>
                                                <p className="text-sm text-gray-500">
                                                    {device.openPorts.slice(0, 3).map(p => p.port).join(', ')}
                                                    {device.openPorts.length > 3 && '...'}
                                                </p>
                                            </div>
                                        )}

                                        {getStatusBadge(device)}
                                    </div>
                                </div>

                                {device.openPorts && device.openPorts.length > 0 && (
                                    <div className="mt-3 ml-8">
                                        <div className="flex flex-wrap gap-2">
                                            {device.openPorts.map((port) => (
                                                <span
                                                    key={`${port.port}-${port.protocol}`}
                                                    className="inline-flex items-center px-2 py-1 text-xs font-medium bg-gray-100 text-gray-800 rounded"
                                                >
                                                    {port.port}/{port.protocol} - {port.service}
                                                </span>
                                            ))}
                                        </div>
                                    </div>
                                )}
                            </div>
                        ))}
                    </div>
                </div>
            )}

            {/* Scanning Indicator */}
            {isScanning && (
                <div className="bg-white rounded-lg shadow p-6">
                    <div className="flex items-center justify-center space-x-3">
                        <Loader2 className="w-6 h-6 animate-spin text-blue-600" />
                        <p className="text-lg font-medium text-gray-900">
                            Scanning network for devices...
                        </p>
                    </div>
                    <div className="mt-2 text-center">
                        <p className="text-sm text-gray-500">
                            This may take a few minutes depending on the network size
                        </p>
                    </div>
                </div>
            )}
        </div>
    );
};

export default NetworkDiscovery;