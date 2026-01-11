// charts.js - функции для работы с графиками Chart.js
export function renderConvergenceChart(canvasRef, iterations, fitnessValues) {
    const ctx = canvasRef.getContext('2d');

    // Уничтожаем старый график, если существует
    if (window.convergenceChart) {
        window.convergenceChart.destroy();
    }

    // Создаем новый график
    window.convergenceChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: iterations,
            datasets: [{
                label: 'Лучший фитнес',
                data: fitnessValues,
                borderColor: '#2196F3',
                backgroundColor: 'rgba(33, 150, 243, 0.1)',
                borderWidth: 2,
                fill: true,
                tension: 0.4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                title: {
                    display: true,
                    text: 'Сходимость алгоритма PSO',
                    font: {
                        size: 16
                    }
                },
                tooltip: {
                    mode: 'index',
                    intersect: false,
                    callbacks: {
                        label: function(context) {
                            return `Фитнес: ${context.parsed.y.toFixed(2)}`;
                        }
                    }
                },
                legend: {
                    display: true,
                    position: 'top'
                }
            },
            scales: {
                x: {
                    title: {
                        display: true,
                        text: 'Итерация'
                    },
                    grid: {
                        display: true
                    }
                },
                y: {
                    title: {
                        display: true,
                        text: 'Фитнес-значение'
                    },
                    grid: {
                        display: true
                    },
                    beginAtZero: false
                }
            },
            interaction: {
                intersect: false,
                mode: 'nearest'
            }
        }
    });
}

export function renderDistributionChart(canvasRef, machineIds, taskCounts) {
    const ctx = canvasRef.getContext('2d');

    // Цвета для разных машин
    const colors = [
        '#FF6384', '#36A2EB', '#FFCE56', '#4BC0C0',
        '#9966FF', '#FF9F40', '#8AC926', '#1982C4'
    ];

    // Уничтожаем старый график, если существует
    if (window.distributionChart) {
        window.distributionChart.destroy();
    }

    window.distributionChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: machineIds.map(id => `VM ${id}`),
            datasets: [{
                label: 'Количество задач',
                data: taskCounts,
                backgroundColor: colors.slice(0, machineIds.length),
                borderColor: colors.slice(0, machineIds.length).map(c => c.replace('0.6', '1')),
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                title: {
                    display: true,
                    text: 'Распределение задач по машинам'
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Количество задач'
                    }
                }
            }
        }
    });
}

// Глобальная функция для скачивания JSON
window.downloadJson = function(jsonData, filename) {
    const blob = new Blob([jsonData], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};