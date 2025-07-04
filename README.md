# WarehouseMaster - Приложение для управления складом

## Описание
Приложение **WarehouseMaster** предназначено для автоматизации учета товаров, контроля их перемещения, управления запасами и генерации отчетов на складе. Оно обеспечивает эффективное управление складскими операциями, минимизирует ошибки ручного учета и повышает прозрачность всех процессов.

---

## Функциональность
1. **Авторизация и регистрация**:
   - Вход в систему по логину и паролю.
   - Регистрация новых пользователей с разграничением прав доступа.

2. **Управление данными**:
   - Просмотр, добавление, редактирование и удаление записей в таблицах базы данных.
   - Поддержка операций CRUD (Create, Read, Update, Delete).

3. **Импорт/экспорт данных**:
   - Экспорт данных в JSON-формат для внешнего использования.
   - Импорт данных из JSON-файлов для обновления базы.

4. **Работа с таблицами**:
   - Выбор таблицы из списка.
   - Фильтрация и сортировка данных.

---

## Установка и запуск
1. **Требования**:
   - .NET 6.0 или выше.
   - PostgreSQL (настройки подключения в файле `appsettings.json`).

2. **Запуск**:
   - Клонируйте репозиторий:
     ```bash
     git clone https://github.com/Blackstarf/WarehouseMaster.git
     ```
   - Откройте решение в Visual Studio.
   - Запустите проект `WarehouseMaster`.

---

## Архитектура
### Паттерны проектирования
- **Repository Pattern**: Инкапсулирует логику работы с базой данных, обеспечивая абстракцию от конкретной СУБД.
- **SOLID**:
  - **Single Responsibility**: Каждый класс отвечает за одну задачу.
  - **Open/Closed**: Классы расширяемы без изменения исходного кода.
  - **Liskov Substitution**: Возможность замены реализаций интерфейсов.
  - **Interface Segregation**: Минимальные специализированные интерфейсы.
  - **Dependency Inversion**: Зависимости строятся на абстракциях.

Скриншоты
![image](https://github.com/user-attachments/assets/7fc751e8-17a5-411a-a856-4df39495d390)
![image](https://github.com/user-attachments/assets/21f26b65-c336-43b7-abc9-97e4ccb2c107)
![image](https://github.com/user-attachments/assets/8288d977-a1fb-43a4-9dfb-659b85fb2dfc)
![image](https://github.com/user-attachments/assets/7de8b720-77d7-41f5-aa45-c45f7a2852d2)
![image](https://github.com/user-attachments/assets/612400e5-024e-44c5-b5f0-cdd656b71a1f)





