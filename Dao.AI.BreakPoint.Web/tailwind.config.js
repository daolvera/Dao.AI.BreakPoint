/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      colors: {
        primary: {
          50: '#e8f5f0',
          100: '#c5e4d6',
          200: '#9fd2ba',
          300: '#79c09e',
          400: '#5cb289',
          500: '#4caf50', // Material Green
          600: '#388e3c',
          700: '#2e7d32',
          800: '#1b5e20',
          900: '#0d5016',
        },
        accent: {
          500: '#ff6b35',
        }
      },
      spacing: {
        '15': '60px',
        '18': '4.5rem',
        '72': '18rem',
        '84': '21rem',
        '96': '24rem',
      },
      fontFamily: {
        'sans': ['Roboto', '"Helvetica Neue"', 'Arial', 'sans-serif'],
      },
      boxShadow: {
        'soft': '0 2px 15px rgba(0, 0, 0, 0.08)',
        'medium': '0 4px 25px rgba(0, 0, 0, 0.15)',
      }
    },
  },
  plugins: [],
  corePlugins: {
    preflight: false, // Disable Tailwind's base styles to avoid conflicts with Angular Material
  }
}
