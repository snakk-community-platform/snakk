/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Pages/**/*.{cshtml,razor}",
    "./Views/**/*.{cshtml,razor}",
    "./wwwroot/js/**/*.js"
  ],
  theme: {
    extend: {},
  },
  plugins: [
    require('daisyui')
  ],
  daisyui: {
    themes: ["light", "dark"],
  },
}

