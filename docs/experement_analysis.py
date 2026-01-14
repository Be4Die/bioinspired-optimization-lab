import json
import matplotlib.pyplot as plt
import numpy as np
from datetime import datetime

# Загрузка данных
with open('experiment_history_20260113_183951.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

experiments = data['Experiments']

# Группировка результатов
configs = {}
for exp in experiments:
    config = exp['Configuration']
    if config not in configs:
        configs[config] = {'PSO': [], 'GA': None}
    
    if exp['AlgorithmType'] == 'Pso':
        configs[config]['PSO'].append(exp)
    elif exp['AlgorithmType'] == 'Genetic' and configs[config]['GA'] is None:
        configs[config]['GA'] = exp

# Сортируем по номеру эксперимента
sorted_configs = sorted(configs.items(), 
                       key=lambda x: int(x[0].split(':')[0].split()[-1]))

# Подготовка данных
config_names = []
pso_avg_makespan = []
pso_min_makespan = []
pso_max_makespan = []
ga_makespan = []

pso_avg_time = []
pso_min_time = []
pso_max_time = []
ga_time = []

task_counts = []

def time_to_seconds(t):
    if not isinstance(t, str):
        return 0
    try:
        h, m, s = t.split(':')
        s, ms = (s.split('.') + ['0'])[:2]
        return int(h)*3600 + int(m)*60 + float(s) + float('0.' + ms)
    except:
        return 0

for config_name, values in sorted_configs:
    num = config_name.split(':')[0].split()[-1]
    name = config_name.split(':', 1)[1].strip()[:20]
    config_names.append(f"{num}: {name}")
    
    # PSO
    if values['PSO']:
        makespans = [e['Makespan'] for e in values['PSO']]
        times = [time_to_seconds(e['ComputationTime']) for e in values['PSO']]
        
        pso_avg_makespan.append(np.mean(makespans))
        pso_min_makespan.append(min(makespans))
        pso_max_makespan.append(max(makespans))
        
        pso_avg_time.append(np.mean(times))
        pso_min_time.append(min(times))
        pso_max_time.append(max(times))
        
        task_counts.append(values['PSO'][0]['TaskCount'])
    
    # GA
    if values['GA']:
        ga_makespan.append(values['GA']['Makespan'])
        ga_time.append(time_to_seconds(values['GA']['ComputationTime']))
    else:
        ga_makespan.append(np.nan)
        ga_time.append(np.nan)

# ----------------------------------------------------
# График 1: Сравнение Makespan (столбцы + диапазон PSO)
# ----------------------------------------------------
plt.figure(figsize=(14, 7))

x = np.arange(len(config_names))
width = 0.35

plt.bar(x - width/2, pso_avg_makespan, width, 
        label='PSO (среднее)', color='cornflowerblue', alpha=0.8)
plt.bar(x + width/2, ga_makespan, width, 
        label='Genetic Algorithm', color='seagreen', alpha=0.8)


plt.xticks(x, config_names, rotation=45, ha='right', fontsize=9)
plt.ylabel('Makespan')
plt.title('Сравнение Makespan: PSO vs Genetic Algorithm')
plt.legend()
plt.grid(True, alpha=0.3, axis='y')
plt.tight_layout()
plt.savefig('makespan_comparison_clean.png', dpi=300)
plt.show()

# ----------------------------------------------------
# График 2: Сравнение времени выполнения
# ----------------------------------------------------
plt.figure(figsize=(14, 7))

plt.bar(x - width/2, pso_avg_time, width, 
        label='PSO (среднее)', color='cornflowerblue', alpha=0.8)
plt.bar(x + width/2, ga_time, width, 
        label='Genetic Algorithm', color='seagreen', alpha=0.8)

plt.xticks(x, config_names, rotation=45, ha='right', fontsize=9)
plt.ylabel('Время выполнения, сек')
plt.title('Сравнение времени выполнения: PSO vs GA')
plt.legend()
plt.grid(True, alpha=0.3, axis='y')
plt.tight_layout()
plt.savefig('time_comparison_clean.png', dpi=300)
plt.show()

# ----------------------------------------------------
# График 3: Зависимость Makespan от кол-ва задач
# ----------------------------------------------------
plt.figure(figsize=(11, 7))

unique_tasks_idx = np.unique(task_counts, return_index=True)[1]
unique_tasks = np.array(task_counts)[unique_tasks_idx]
unique_pso = np.array(pso_avg_makespan)[unique_tasks_idx]
unique_ga = np.array(ga_makespan)[unique_tasks_idx]

plt.plot(unique_tasks, unique_pso, 'o-', color='cornflowerblue', 
         linewidth=2, markersize=8, label='PSO (среднее)')
plt.plot(unique_tasks, unique_ga, 's-', color='seagreen', 
         linewidth=2, markersize=8, label='Genetic Algorithm')

plt.xlabel('Количество задач')
plt.ylabel('Makespan')
plt.title('Зависимость Makespan от количества задач')
plt.legend()
plt.grid(True, alpha=0.3)
plt.tight_layout()
plt.savefig('makespan_vs_task_count.png', dpi=300)
plt.show()

# ----------------------------------------------------
# График 4: Соотношение PSO / GA по makespan
# ----------------------------------------------------
plt.figure(figsize=(12, 6))

valid = ~np.isnan(ga_makespan)
ratio = np.array(pso_avg_makespan)[valid] / np.array(ga_makespan)[valid]

plt.bar(range(len(ratio)), ratio, color='coral', alpha=0.8)
plt.axhline(1.0, color='darkred', linestyle='--', linewidth=1.4, alpha=0.7)

plt.xticks(range(len(ratio)), [str(i+1) for i in np.where(valid)[0]], fontsize=9)
plt.ylabel('PSO / GA (makespan)')
plt.title('Соотношение качества решений (PSO / GA)\n> 1 — GA лучше, < 1 — PSO лучше')
plt.grid(True, alpha=0.3, axis='y')
plt.tight_layout()
plt.savefig('ratio_makespan.png', dpi=300)
plt.show()