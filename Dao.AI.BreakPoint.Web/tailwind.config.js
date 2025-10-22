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
          500: '#058743', // Your main primary color
          600: '#047a3d',
          700: '#036b36',
          800: '#025c2f',
          900: '#014421',
        },
        // You can also add other custom colors here
        accent: {
          500: '#ff6b35', // Example accent color
        }
      },
      spacing: {
        '15': '60px', // For header/footer heights
      },
      fontFamily: {
        'roboto': ['Roboto', '"Helvetica Neue"', 'sans-serif'],
      }
    },
  },
  plugins: [],
}
