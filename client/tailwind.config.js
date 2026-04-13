/** @type {import('tailwindcss').Config} */
export default {
  darkMode: 'class',
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Cairo', 'sans-serif']
      },
      colors: {
        brand: {
          50: '#f0fdff',
          100: '#ccfbf1',
          200: '#99f6e4',
          300: '#5eead4',
          400: '#2dd4bf',
          500: '#14b8a6',
          600: '#0f766e',
          700: '#115e59',
          800: '#134e4a',
          900: '#042f2e'
        }
      },
      boxShadow: {
        glow: '0 24px 90px rgba(20, 184, 166, 0.18)'
      },
      backgroundImage: {
        'hero-radial': 'radial-gradient(circle at top, rgba(20,184,166,0.18), transparent 40%), radial-gradient(circle at bottom right, rgba(245,158,11,0.14), transparent 32%)'
      }
    }
  },
  plugins: []
};
