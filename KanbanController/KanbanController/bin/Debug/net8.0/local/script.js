		const API_URL = 'http://192.168.1.2:2000/api/kanban/tasks';
		let currentColumn = null;
let currentTask = null;
let tasks = [];

// Инициализация
document.addEventListener('DOMContentLoaded', async () => {
    await loadTasksFromAPI();
    setupContextMenu();
});

// Загрузка задач с API
async function loadTasksFromAPI() {
    try {
        const response = await fetch(API_URL);
        if (!response.ok) {
            throw new Error('Ошибка загрузки задач');
        }
        const data = await response.json();

        // Проверяем, что data является массивом
        if (Array.isArray(data)) {
            tasks = data;
            tasks.forEach(task => createTaskElement(task));
            showNotification('Задачи загружены');
        } else {
            throw new Error('Некорректный формат данных: ожидался массив задач');
        }
    } catch (error) {
        showNotification('Ошибка загрузки задач', true);
        console.error(error);
    }
}

// Сохранение задачи в API
async function saveTaskToAPI(task) {
    try {
        const method = task.id ? 'PUT' : 'POST';
        const url = task.id ? `${API_URL}/${task.id}` : API_URL;

        const response = await fetch(url, {
            method: method,
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(task),
        });
        if (!response.ok) {
            throw new Error('Ошибка сохранения задачи');
        }
        return await response.json();
    } catch (error) {
        showNotification('Ошибка сохранения задачи', true);
        console.error(error);
    }
}

// Удаление задачи из API
async function deleteTaskFromAPI(taskId) {
    try {
        const response = await fetch(`${API_URL}/${taskId}`, {
            method: 'DELETE',
        });
        if (!response.ok) {
            throw new Error('Ошибка удаления задачи');
        }
        showNotification('Задача удалена');
    } catch (error) {
        showNotification('Ошибка удаления задачи', true);
        console.error(error);
    }
}

// Создание элемента задачи
function createTaskElement(taskData) {
    const task = document.createElement('div');
    task.className = `task priority-${taskData.priority}`;
    task.draggable = true;
    task.textContent = taskData.text;
    task.id = taskData.id;
    task.dataset.priority = taskData.priority;
    task.ondragstart = dragStart;
    task.ondragend = dragEnd;

    document.querySelector(`#${taskData.status} .tasks`).appendChild(task);
}

// Drag and Drop
function allowDrop(ev) {
    ev.preventDefault();
}

function dragStart(ev) {
    ev.dataTransfer.setData("text", ev.target.id);
    ev.target.classList.add('dragging');
}

function dragEnd(ev) {
    ev.target.classList.remove('dragging');
}

async function drop(ev) {
    ev.preventDefault();
    const taskId = ev.dataTransfer.getData("text");
    const taskElement = document.getElementById(taskId);
    const targetColumn = ev.target.closest('.tasks');

    if (targetColumn && taskElement) {
        targetColumn.appendChild(taskElement);
        const newStatus = targetColumn.parentElement.id;
        const task = tasks.find(t => t.id === taskId);
        if (task) {
            task.status = newStatus;
            await saveTaskToAPI(task);
            showNotification('Задача перемещена');
        }
    }
}

// Добавление задачи
async function addTask() {
    const input = document.getElementById('taskInput');
    const priority = document.getElementById('taskPriority').value;
    
    if (input.value.trim()) {
        const taskData = {
            text: input.value,
            priority: priority,
            status: currentColumn
        };

        const savedTask = await saveTaskToAPI(taskData);
        if (savedTask) {
            tasks.push(savedTask);
            createTaskElement(savedTask);
            input.value = '';
            closeModal();
            showNotification('Задача добавлена');
        }
    }
}

// Редактировать задачу
async function editTask() {
    const newText = prompt('Введите новый текст задачи:', currentTask.textContent);
    if (newText) {
        currentTask.textContent = newText;
        const task = tasks.find(t => t.id === currentTask.id);
        if (task) {
            task.text = newText;
            await saveTaskToAPI(task);
            showNotification('Задача обновлена');
        }
    }
    hideContextMenu();
}

// Изменить приоритет задачи
async function changePriority(priority) {
    currentTask.className = `task priority-${priority}`;
    currentTask.dataset.priority = priority;
    const task = tasks.find(t => t.id === currentTask.id);
    if (task) {
        task.priority = priority;
        await saveTaskToAPI(task);
        showNotification('Приоритет изменён');
    }
    hideContextMenu();
}

// Удалить задачу
async function deleteCurrentTask() {
    if (confirm('Вы уверены, что хотите удалить задачу?')) {
        await deleteTaskFromAPI(currentTask.id);
        tasks = tasks.filter(t => t.id !== currentTask.id);
        currentTask.remove();
        showNotification('Задача удалена');
    }
    hideContextMenu();
}

// Настройка контекстного меню
function setupContextMenu() {
    document.addEventListener('click', () => {
        hideContextMenu();
    });

    document.addEventListener('contextmenu', (event) => {
        if (event.target.classList.contains('task')) {
            event.preventDefault();
            showContextMenu(event);
        }
    });
}

// Показать контекстное меню
function showContextMenu(event) {
    currentTask = event.target;
    const contextMenu = document.getElementById('contextMenu');
    contextMenu.style.display = 'block';
    contextMenu.style.left = `${event.pageX}px`;
    contextMenu.style.top = `${event.pageY}px`;
}

// Скрыть контекстное меню
function hideContextMenu() {
    const contextMenu = document.getElementById('contextMenu');
    contextMenu.style.display = 'none';
}

// Уведомления
function showNotification(text, isError = false) {
    const notification = document.getElementById('notification');
    notification.textContent = text;
    notification.style.background = isError ? '#F44336' : '#4CAF50';
    notification.style.display = 'block';
    setTimeout(() => notification.style.display = 'none', 3000);
}

// Показ модального окна
function showModal(columnId) {
    currentColumn = columnId;
    document.getElementById('modal').style.display = 'flex';
}

// Закрытие модального окна
function closeModal() {
    document.getElementById('modal').style.display = 'none';
    currentColumn = null;
}

// Закрытие модального окна при клике вне его
window.onclick = function(event) {
    const modal = document.getElementById('modal');
    if (event.target === modal) {
        closeModal();
    }
};