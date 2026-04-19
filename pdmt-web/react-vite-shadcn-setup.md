# ========================================
# 1. СОЗДАНИЕ BOILERPLATE
# ========================================

npm create vite@latest pdmt-web -- --template react-ts
cd pdmt-web

# ========================================
# 2. УСТАНОВКА ОСНОВНЫХ ЗАВИСИМОСТЕЙ
# ========================================

npm install react-router-dom        # v7
npm install recharts                # графики
npm install zod                     # валидация схем
npm install react-error-boundary    # Error Boundary компонент

# ========================================
# 3. УСТАНОВКА УТИЛИТ ДЛЯ СТИЛЕЙ
# ========================================

# Устанавливаются автоматически npx shadcn@latest init,
# но можно явно добавить в зависимости заранее:

npm install class-variance-authority
npm install clsx
npm install tailwind-merge
npm install lucide-react

# ========================================
# 4. УСТАНОВКА CSS FRAMEWORK
# ========================================

npm install -D tailwindcss@3 postcss autoprefixer

# ========================================
# 5. УСТАНОВКА DEVELOPER TOOLS
# ========================================

# eslint, @eslint/js, typescript-eslint и ESLint-плагины
# уже включены Vite в шаге 1 — переустанавливать не нужно

npm install -D @vitejs/plugin-basic-ssl
npm install -D @types/node         # нужен для path alias в vite.config.ts

# Тестирование
npm install -D vitest jsdom
npm install -D @testing-library/react @testing-library/user-event @testing-library/jest-dom

# ========================================
# 6. СОЗДАНИЕ ФАЙЛОВ КОНФИГОВ
# ========================================

# Созданы автоматически (шаг 1):
# - vite.config.ts  — подключает плагин react
# - eslint.config.js — правила линтера
# - index.html      — HTML вход
# - .gitignore      — исключения git

# Созданы через CLI:
npx tailwindcss init -p
# tailwind.config.js — настройки Tailwind (content, theme, plugins)
# postcss.config.js  — обработка CSS

npx shadcn@latest init
# components.json — Shadcn CLI конфиг (пути к компонентам, стиль, базовый цвет)
# Модифицирует tailwind.config.js и src/index.css

# Ручное редактирование:
# vite.config.ts    — добавить плагины (react, basicSsl), alias @/, test.environment jsdom
# tsconfig.json     — baseUrl, paths
# tsconfig.app.json — lib, paths
# tsconfig.node.json — конфиг для vite.config.ts

# ========================================
# 7. СОЗДАНИЕ СТРУКТУРЫ SRC/ (вручную)
# ========================================

# src/main.tsx          — React.createRoot
# src/App.tsx           — роутинг
# src/index.css         — Tailwind глобальные стили
# src/config.ts         — конфиг приложения (env vars)
# src/lib/utils.ts      — утилиты (cn())
# src/auth/             — контекст, провайдер, хук
# src/api/              — HTTP клиент и эндпоинты
# src/hooks/            — кастомные хуки
# src/components/       — бизнес компоненты
# src/components/ui/    — Shadcn UI компоненты (генерируются CLI)
# src/pages/            — страницы приложения
# src/test/             — тестовые утилиты и setup

# ========================================
# 8. СОЗДАНИЕ КОНФИГУРАЦИИ API
# ========================================

# Создать файлы (Unix / Git Bash):
touch .env
touch .env.example
touch .env.production
touch src/config.ts

# Заполнить .env:
# VITE_PDMT_API_BASE_URL=https://localhost:7031

# Заполнить .env.example:
# VITE_PDMT_API_BASE_URL=https://your-api.example.com

# Заполнить .env.production:
# VITE_PDMT_API_BASE_URL=https://your-prod-api.example.com

# Написать src/config.ts:
# export const config = { api: { baseUrl: ... } }

# ========================================
# 9. ОБНОВЛЕНИЕ API КЛИЕНТОВ
# ========================================

# src/api/client.ts — добавить import config, использовать baseUrl
# src/api/auth.ts   — добавить import config, использовать baseUrl

# ========================================
# 10. VS CODE КОНФИГУРАЦИЯ
# ========================================

# Создать .vscode/settings.json:
# - Скрыть node_modules, dist, package-lock.json из проводника и поиска
# - Настроить TypeScript SDK пути
# - Включить auto-format для TypeScript/TSX
# - Prettier как default formatter

# ========================================
# 11. ГОТОВО К РАБОТЕ
# ========================================

npm install          # Финальная установка всех зависимостей
npm run dev          # Запустить dev сервер на https://localhost:5173
npm run test         # Запустить тесты (vitest watch)
npm run test:run     # Запустить тесты однократно

# ========================================
# 12. ДОБАВЛЕНИЕ КОМПОНЕНТОВ SHADCN
# ========================================

# Команды для добавления нужных компонентов (по мере разработки).
# Radix UI зависимости устанавливаются автоматически вместе с компонентом.

npx shadcn@latest add button
npx shadcn@latest add card
npx shadcn@latest add dialog
npx shadcn@latest add input
npx shadcn@latest add label
npx shadcn@latest add select
npx shadcn@latest add separator
npx shadcn@latest add slider

# Компоненты скопируются в src/components/ui/ и будут готовы к использованию
