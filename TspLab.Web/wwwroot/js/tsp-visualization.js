// TSP Visualization JavaScript Functions
// Chart.js instance for convergence chart
let convergenceChart = null;

// Initialize Chart.js configuration
function initializeChart(canvasId) {
    const ctx = document.getElementById(canvasId);
    if (!ctx) return null;
    
    return new Chart(ctx, {
        type: 'line',
        data: {
            labels: [],
            datasets: [{
                label: 'Best Fitness',
                data: [],
                borderColor: 'rgb(59, 130, 246)',
                backgroundColor: 'rgba(59, 130, 246, 0.1)',
                borderWidth: 2,
                tension: 0.1,
                fill: true
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                x: {
                    display: true,
                    title: {
                        display: true,
                        text: 'Generation'
                    }
                },
                y: {
                    display: true,
                    title: {
                        display: true,
                        text: 'Fitness (1/Distance)'
                    }
                }
            },
            plugins: {
                legend: {
                    display: true,
                    position: 'top'
                },
                tooltip: {
                    enabled: true,
                    mode: 'index',
                    intersect: false
                }
            },
            interaction: {
                mode: 'nearest',
                axis: 'x',
                intersect: false
            }
        }
    });
}

// Draw cities on canvas
function drawCities(canvasId, cities) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    
    const ctx = canvas.getContext('2d');
    const width = canvas.width;
    const height = canvas.height;
    
    // Clear canvas
    ctx.clearRect(0, 0, width, height);
    
    // Set canvas background
    ctx.fillStyle = '#f8f9fa';
    ctx.fillRect(0, 0, width, height);
    
    // Draw border
    ctx.strokeStyle = '#dee2e6';
    ctx.lineWidth = 2;
    ctx.strokeRect(0, 0, width, height);
    
    if (!cities || cities.length === 0) return;
    
    // Find bounds for scaling
    const padding = 50;
    const drawWidth = width - 2 * padding;
    const drawHeight = height - 2 * padding;
    
    const minX = Math.min(...cities.map(c => c.x));
    const maxX = Math.max(...cities.map(c => c.x));
    const minY = Math.min(...cities.map(c => c.y));
    const maxY = Math.max(...cities.map(c => c.y));
    
    const scaleX = drawWidth / (maxX - minX || 1);
    const scaleY = drawHeight / (maxY - minY || 1);
    const scale = Math.min(scaleX, scaleY);
    
    // Calculate offset to center the cities
    const offsetX = (width - (maxX - minX) * scale) / 2 - minX * scale;
    const offsetY = (height - (maxY - minY) * scale) / 2 - minY * scale;
    
    // Draw cities
    cities.forEach((city, index) => {
        const x = city.x * scale + offsetX;
        const y = city.y * scale + offsetY;
        
        // Draw city circle
        ctx.beginPath();
        ctx.arc(x, y, 6, 0, 2 * Math.PI);
        ctx.fillStyle = '#3b82f6';
        ctx.fill();
        ctx.strokeStyle = '#1e40af';
        ctx.lineWidth = 2;
        ctx.stroke();
        
        // Draw city label
        ctx.fillStyle = '#1f2937';
        ctx.font = '12px Arial';
        ctx.textAlign = 'center';
        ctx.fillText(index.toString(), x, y - 10);
    });
}

// Draw tour on canvas
function drawTour(canvasId, tourPoints) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    
    const ctx = canvas.getContext('2d');
    const width = canvas.width;
    const height = canvas.height;
    
    // Clear canvas
    ctx.clearRect(0, 0, width, height);
    
    // Set canvas background
    ctx.fillStyle = '#f8f9fa';
    ctx.fillRect(0, 0, width, height);
    
    // Draw border
    ctx.strokeStyle = '#dee2e6';
    ctx.lineWidth = 2;
    ctx.strokeRect(0, 0, width, height);
    
    if (!tourPoints || tourPoints.length === 0) return;
    
    // Find bounds for scaling
    const padding = 50;
    const drawWidth = width - 2 * padding;
    const drawHeight = height - 2 * padding;
    
    const minX = Math.min(...tourPoints.map(p => p.x));
    const maxX = Math.max(...tourPoints.map(p => p.x));
    const minY = Math.min(...tourPoints.map(p => p.y));
    const maxY = Math.max(...tourPoints.map(p => p.y));
    
    const scaleX = drawWidth / (maxX - minX || 1);
    const scaleY = drawHeight / (maxY - minY || 1);
    const scale = Math.min(scaleX, scaleY);
    
    // Calculate offset to center the tour
    const offsetX = (width - (maxX - minX) * scale) / 2 - minX * scale;
    const offsetY = (height - (maxY - minY) * scale) / 2 - minY * scale;
    
    // Convert tour points to canvas coordinates
    const canvasPoints = tourPoints.map(p => ({
        x: p.x * scale + offsetX,
        y: p.y * scale + offsetY
    }));
    
    // Draw tour lines
    if (canvasPoints.length > 1) {
        ctx.beginPath();
        ctx.moveTo(canvasPoints[0].x, canvasPoints[0].y);
        
        for (let i = 1; i < canvasPoints.length; i++) {
            ctx.lineTo(canvasPoints[i].x, canvasPoints[i].y);
        }
        
        // Close the tour
        ctx.lineTo(canvasPoints[0].x, canvasPoints[0].y);
        
        ctx.strokeStyle = '#ef4444';
        ctx.lineWidth = 2;
        ctx.stroke();
    }
    
    // Draw cities
    canvasPoints.forEach((point, index) => {
        // Draw city circle
        ctx.beginPath();
        ctx.arc(point.x, point.y, 6, 0, 2 * Math.PI);
        ctx.fillStyle = '#3b82f6';
        ctx.fill();
        ctx.strokeStyle = '#1e40af';
        ctx.lineWidth = 2;
        ctx.stroke();
        
        // Draw city label
        ctx.fillStyle = '#1f2937';
        ctx.font = '12px Arial';
        ctx.textAlign = 'center';
        ctx.fillText(index.toString(), point.x, point.y - 10);
    });
    
    // Draw start/end indicator
    if (canvasPoints.length > 0) {
        const startPoint = canvasPoints[0];
        ctx.beginPath();
        ctx.arc(startPoint.x, startPoint.y, 10, 0, 2 * Math.PI);
        ctx.strokeStyle = '#10b981';
        ctx.lineWidth = 3;
        ctx.stroke();
        
        // Draw "START" label
        ctx.fillStyle = '#10b981';
        ctx.font = 'bold 10px Arial';
        ctx.textAlign = 'center';
        ctx.fillText('START', startPoint.x, startPoint.y + 20);
    }
}

// Update convergence chart
function updateConvergenceChart(canvasId, data) {
    if (!convergenceChart) {
        convergenceChart = initializeChart(canvasId);
    }
    
    if (!convergenceChart || !data) return;
    
    // Update chart data
    convergenceChart.data.labels = data.map((_, index) => index + 1);
    convergenceChart.data.datasets[0].data = data.map(d => d.fitness);
    
    // Update chart
    convergenceChart.update('none'); // No animation for real-time updates
}

// Utility function to calculate distance between two points
function calculateDistance(point1, point2) {
    const dx = point1.x - point2.x;
    const dy = point1.y - point2.y;
    return Math.sqrt(dx * dx + dy * dy);
}

// Calculate total tour distance
function calculateTourDistance(tourPoints) {
    if (!tourPoints || tourPoints.length < 2) return 0;
    
    let totalDistance = 0;
    for (let i = 0; i < tourPoints.length - 1; i++) {
        totalDistance += calculateDistance(tourPoints[i], tourPoints[i + 1]);
    }
    
    // Add distance back to start
    totalDistance += calculateDistance(tourPoints[tourPoints.length - 1], tourPoints[0]);
    
    return totalDistance;
}

// Animation function for smooth tour updates
function animateTour(canvasId, fromTour, toTour, duration = 1000) {
    if (!fromTour || !toTour || fromTour.length !== toTour.length) {
        drawTour(canvasId, toTour);
        return;
    }
    
    const startTime = Date.now();
    
    function animate() {
        const elapsed = Date.now() - startTime;
        const progress = Math.min(elapsed / duration, 1);
        
        // Interpolate between tours
        const interpolatedTour = fromTour.map((fromPoint, index) => {
            const toPoint = toTour[index];
            return {
                x: fromPoint.x + (toPoint.x - fromPoint.x) * progress,
                y: fromPoint.y + (toPoint.y - fromPoint.y) * progress
            };
        });
        
        drawTour(canvasId, interpolatedTour);
        
        if (progress < 1) {
            requestAnimationFrame(animate);
        }
    }
    
    animate();
}

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    console.log('TSP Visualization JavaScript loaded');
});
