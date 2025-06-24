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
// Missing function: AddAllCertificates

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
