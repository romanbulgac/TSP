// TSP Visualization JavaScript Functions
// Chart.js instance for convergence chart
let convergenceChart = null;

// Benchmark charts
let benchmarkCharts = {};

// Initialize Chart.js configuration
function initializeChart(canvasId) {
    const ctx = document.getElementById(canvasId);
    if (!ctx) {
        console.error('Canvas element not found:', canvasId);
        return null;
    }
    
    // Check if Chart.js is available
    if (typeof Chart === 'undefined') {
        console.error('Chart.js library is not loaded');
        return null;
    }
    
    try {
        return new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [{
                    label: 'Best Fitness',
                    data: [],
                    borderColor: 'rgb(59, 130, 246)',
                    backgroundColor: 'rgba(59, 130, 246, 0.1)',
                    borderWidth: 3,
                    tension: 0.2,
                    fill: true,
                    pointRadius: 2,
                    pointHoverRadius: 5
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: {
                    duration: 300,
                    easing: 'easeInOutQuart'
                },
                scales: {
                    x: {
                        display: true,
                        title: {
                            display: true,
                            text: 'Generation',
                            color: '#374151',
                            font: {
                                size: 12,
                                weight: 'bold'
                            }
                        },
                        grid: {
                            color: 'rgba(156, 163, 175, 0.3)'
                        },
                        ticks: {
                            color: '#6B7280'
                        }
                    },
                    y: {
                        display: true,
                        title: {
                            display: true,
                            text: 'Fitness (1000/Distance)',
                            color: '#374151',
                            font: {
                                size: 12,
                                weight: 'bold'
                            }
                        },
                        grid: {
                            color: 'rgba(156, 163, 175, 0.3)'
                        },
                        ticks: {
                            color: '#6B7280',
                            callback: function(value) {
                            return value.toFixed(3);
                        }
                    }
                }
            },
            plugins: {
                legend: {
                    display: true,
                    position: 'top',
                    labels: {
                        color: '#374151',
                        font: {
                            size: 11,
                            weight: '500'
                        },
                        usePointStyle: true,
                        pointStyle: 'line'
                    }
                },
                tooltip: {
                    enabled: true,
                    mode: 'index',
                    intersect: false,
                    backgroundColor: 'rgba(55, 65, 81, 0.9)',
                    titleColor: 'white',
                    bodyColor: 'white',
                    borderColor: 'rgb(59, 130, 246)',
                    borderWidth: 1,
                    callbacks: {
                        title: function(context) {
                            return 'Generation: ' + context[0].label;
                        },
                        label: function(context) {
                            const value = context.parsed.y;
                            const distance = (1000 / value).toFixed(2);
                            return `${context.dataset.label}: ${value.toFixed(4)} (Distance: ${distance})`;
                        }
                    }
                }
            },
            interaction: {
                mode: 'nearest',
                axis: 'x',
                intersect: false
            },
            elements: {
                line: {
                    tension: 0.2
                },
                point: {
                    hoverBackgroundColor: 'rgb(59, 130, 246)',
                    hoverBorderColor: 'white'
                }
            }
        }
    });
    } catch (error) {
        console.error('Error initializing chart:', error);
        return null;
    }
}

// Draw cities on canvas
function drawCities(canvasId, cities) {
    console.log('drawCities called with:', canvasId, cities);
    
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        console.error('Canvas not found:', canvasId);
        return;
    }
    
    const ctx = canvas.getContext('2d');
    const width = canvas.width;
    const height = canvas.height;
    
    console.log('Canvas dimensions:', width, height);
    
    // Clear canvas
    ctx.clearRect(0, 0, width, height);
    
    // Set canvas background
    ctx.fillStyle = '#f8f9fa';
    ctx.fillRect(0, 0, width, height);
    
    // Draw border
    ctx.strokeStyle = '#dee2e6';
    ctx.lineWidth = 2;
    ctx.strokeRect(0, 0, width, height);
    
    if (!cities || cities.length === 0) {
        console.log('No cities to draw');
        return;
    }
    
    console.log('Drawing', cities.length, 'cities');
    
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
    
    console.log('Cities drawn successfully');
}

// Draw tour on canvas
function drawTour(canvasId, tourPoints) {
    console.log('drawTour called with:', canvasId, tourPoints);
    
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        console.error('Canvas not found:', canvasId);
        return;
    }
    
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
    
    if (!tourPoints || tourPoints.length === 0) {
        console.log('No tour points to draw');
        return;
    }
    
    console.log('Drawing tour with', tourPoints.length, 'points');
    
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
    
    console.log('Tour drawn successfully');
}

// Update convergence chart
function updateConvergenceChart(canvasId, data) {
    console.log('updateConvergenceChart called with:', canvasId, data);
    
    // Check if Chart.js is available
    if (typeof Chart === 'undefined') {
        console.error('Chart.js library is not loaded');
        return;
    }
    
    if (!convergenceChart) {
        console.log('Initializing convergence chart...');
        convergenceChart = initializeChart(canvasId);
        if (!convergenceChart) {
            console.error('Failed to initialize convergence chart - check if canvas exists and Chart.js is loaded');
            return;
        }
    }
    
    if (!data || !Array.isArray(data) || data.length === 0) {
        console.error('Invalid data for convergence chart:', data);
        return;
    }
    
    // Handle both array of numbers and array of objects
    let fitnessData;
    if (typeof data[0] === 'number') {
        // Array of numbers
        fitnessData = data;
    } else if (typeof data[0] === 'object' && data[0].fitness !== undefined) {
        // Array of objects with fitness property
        fitnessData = data.map(d => d.fitness);
    } else {
        console.error('Invalid data format for convergence chart. Expected numbers or objects with fitness property.');
        return;
    }
    
    console.log('Processing fitness data:', fitnessData);
    
    // Update chart data with generation labels
    const generations = fitnessData.map((_, index) => index + 1);
    convergenceChart.data.labels = generations;
    convergenceChart.data.datasets[0].data = fitnessData;
    
    // Add average fitness line if we have enough data points
    if (fitnessData.length > 10) {
        const windowSize = Math.min(10, Math.floor(fitnessData.length / 4));
        const movingAverage = calculateMovingAverage(fitnessData, windowSize);
        
        // Check if average dataset exists, if not create it
        if (convergenceChart.data.datasets.length === 1) {
            convergenceChart.data.datasets.push({
                label: 'Moving Average',
                data: movingAverage,
                borderColor: 'rgb(16, 185, 129)',
                backgroundColor: 'rgba(16, 185, 129, 0.1)',
                borderWidth: 2,
                tension: 0.3,
                fill: false,
                pointRadius: 0
            });
        } else {
            convergenceChart.data.datasets[1].data = movingAverage;
        }
    }
    
    // Add convergence detection and visual indicators
    if (fitnessData.length > 20) {
        const lastTenValues = fitnessData.slice(-10);
        const convergenceThreshold = 0.001; // 0.1% change threshold
        const isConverged = checkConvergence(lastTenValues, convergenceThreshold);
        
        if (isConverged) {
            // Add convergence point annotation
            addConvergenceAnnotation(convergenceChart, fitnessData.length - 10);
        }
    }
    
    // Smooth update with minimal animation for real-time updates
    convergenceChart.update('none');
    console.log('Chart updated successfully with', fitnessData.length, 'data points');
}

// Helper function to calculate moving average
function calculateMovingAverage(data, windowSize) {
    const result = [];
    for (let i = 0; i < data.length; i++) {
        const start = Math.max(0, i - windowSize + 1);
        const window = data.slice(start, i + 1);
        const average = window.reduce((sum, val) => sum + val, 0) / window.length;
        result.push(average);
    }
    return result;
}

// Helper function to check convergence
function checkConvergence(values, threshold) {
    if (values.length < 2) return false;
    
    const maxValue = Math.max(...values);
    const minValue = Math.min(...values);
    const relativeChange = (maxValue - minValue) / maxValue;
    
    return relativeChange < threshold;
}

// Helper function to add convergence annotation
function addConvergenceAnnotation(chart, convergencePoint) {
    // Simple convergence indicator - change border color of the dataset
    chart.data.datasets[0].borderColor = 'rgb(16, 185, 129)'; // Green when converged
    
    // Could be extended to add actual annotations if Chart.js annotation plugin is available
    console.log('Convergence detected at generation:', convergencePoint);
}

// Clear convergence chart
function clearConvergenceChart() {
    if (convergenceChart) {
        convergenceChart.data.labels = [];
        convergenceChart.data.datasets.forEach(dataset => {
            dataset.data = [];
        });
        
        // Reset to original blue color
        convergenceChart.data.datasets[0].borderColor = 'rgb(59, 130, 246)';
        
        // Remove moving average dataset if it exists
        if (convergenceChart.data.datasets.length > 1) {
            convergenceChart.data.datasets.splice(1, 1);
        }
        
        convergenceChart.update();
        console.log('Convergence chart cleared');
    }
}

// Reset convergence chart (destroy and recreate)
function resetConvergenceChart(canvasId) {
    if (convergenceChart) {
        convergenceChart.destroy();
        convergenceChart = null;
        console.log('Convergence chart destroyed');
    }
    
    // Reinitialize if canvas exists
    const canvas = document.getElementById(canvasId);
    if (canvas) {
        convergenceChart = initializeChart(canvasId);
        console.log('Convergence chart reinitialized');
    }
}

// Update benchmark comparison chart
function updateBenchmarkChart(canvasId, data, yAxisLabel) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    
    // Destroy existing chart if it exists
    if (benchmarkCharts[canvasId]) {
        benchmarkCharts[canvasId].destroy();
    }
    
    const ctx = canvas.getContext('2d');
    
    // Prepare data for Chart.js
    const labels = data.map(d => d.algorithm);
    const values = yAxisLabel === 'Distance' ? 
        data.map(d => d.avgDistance) : 
        data.map(d => d.avgTime);
    
    benchmarkCharts[canvasId] = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: yAxisLabel,
                data: values,
                backgroundColor: [
                    'rgba(59, 130, 246, 0.7)',
                    'rgba(16, 185, 129, 0.7)',
                    'rgba(245, 158, 11, 0.7)',
                    'rgba(239, 68, 68, 0.7)',
                    'rgba(139, 92, 246, 0.7)',
                    'rgba(236, 72, 153, 0.7)'
                ],
                borderColor: [
                    'rgb(59, 130, 246)',
                    'rgb(16, 185, 129)',
                    'rgb(245, 158, 11)',
                    'rgb(239, 68, 68)',
                    'rgb(139, 92, 246)',
                    'rgb(236, 72, 153)'
                ],
                borderWidth: 2
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
                        text: 'Algorithm'
                    }
                },
                y: {
                    display: true,
                    title: {
                        display: true,
                        text: yAxisLabel
                    },
                    beginAtZero: true
                }
            },
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    enabled: true,
                    callbacks: {
                        label: function(context) {
                            const value = yAxisLabel === 'Distance' ? 
                                context.parsed.y.toFixed(2) : 
                                context.parsed.y.toFixed(3) + 's';
                            return yAxisLabel + ': ' + value;
                        }
                    }
                }
            }
        }
    });
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

// Benchmark chart functions
function updateBenchmarkChart(canvasId, data, yAxisLabel) {
    const ctx = document.getElementById(canvasId);
    if (!ctx) {
        console.error(`Canvas element ${canvasId} not found`);
        return;
    }
    
    // Destroy existing chart if it exists
    if (benchmarkCharts[canvasId]) {
        benchmarkCharts[canvasId].destroy();
    }
    
    // Prepare data based on chart type
    let chartData;
    let chartType = 'bar';
    
    if (yAxisLabel.includes('Distance')) {
        // Distance comparison chart - show both average and best
        chartData = {
            labels: data.map(d => d.algorithm),
            datasets: [
                {
                    label: 'Average Distance',
                    data: data.map(d => d.avgDistance),
                    backgroundColor: 'rgba(59, 130, 246, 0.6)',
                    borderColor: 'rgb(59, 130, 246)',
                    borderWidth: 1
                },
                {
                    label: 'Best Distance',
                    data: data.map(d => d.bestDistance),
                    backgroundColor: 'rgba(16, 185, 129, 0.6)',
                    borderColor: 'rgb(16, 185, 129)',
                    borderWidth: 1
                }
            ]
        };
    } else {
        // Time comparison chart
        chartData = {
            labels: data.map(d => d.algorithm),
            datasets: [{
                label: yAxisLabel,
                data: data.map(d => d.avgTime),
                backgroundColor: 'rgba(168, 85, 247, 0.6)',
                borderColor: 'rgb(168, 85, 247)',
                borderWidth: 1
            }]
        };
    }
    
    // Create new chart
    benchmarkCharts[canvasId] = new Chart(ctx, {
        type: chartType,
        data: chartData,
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                title: {
                    display: true,
                    text: yAxisLabel.includes('Distance') ? 'Algorithm Distance Comparison' : 'Algorithm Time Comparison',
                    color: '#374151',
                    font: {
                        size: 14,
                        weight: 'bold'
                    }
                },
                legend: {
                    display: yAxisLabel.includes('Distance'),
                    position: 'top'
                },
                tooltip: {
                    backgroundColor: 'rgba(0, 0, 0, 0.8)',
                    titleColor: '#ffffff',
                    bodyColor: '#ffffff',
                    borderColor: '#6b7280',
                    borderWidth: 1
                }
            },
            scales: {
                x: {
                    title: {
                        display: true,
                        text: 'Algorithm',
                        color: '#374151',
                        font: {
                            size: 12,
                            weight: 'bold'
                        }
                    },
                    ticks: {
                        color: '#6b7280',
                        maxRotation: 45,
                        minRotation: 0
                    },
                    grid: {
                        color: 'rgba(156, 163, 175, 0.3)'
                    }
                },
                y: {
                    title: {
                        display: true,
                        text: yAxisLabel,
                        color: '#374151',
                        font: {
                            size: 12,
                            weight: 'bold'
                        }
                    },
                    ticks: {
                        color: '#6b7280',
                        callback: function(value, index, values) {
                            if (yAxisLabel.includes('Distance')) {
                                return value.toFixed(1);
                            } else {
                                return value.toFixed(2) + 's';
                            }
                        }
                    },
                    grid: {
                        color: 'rgba(156, 163, 175, 0.3)'
                    },
                    beginAtZero: true
                }
            },
            animation: {
                duration: 800,
                easing: 'easeInOutQuart'
            }
        }
    });
    
    console.log(`Updated benchmark chart: ${canvasId}`);
}

// Clear all benchmark charts
function clearBenchmarkCharts() {
    Object.keys(benchmarkCharts).forEach(chartId => {
        if (benchmarkCharts[chartId]) {
            benchmarkCharts[chartId].destroy();
            delete benchmarkCharts[chartId];
        }
    });
}
