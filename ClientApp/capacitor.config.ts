import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'pt.mapaemsal.app',
  appName: 'MapaMensal',
  webDir: 'www',
  server: {
    androidScheme: 'https',
    allowNavigation: ['restpje-gzacbrbxbjbwe0ga.brazilsouth-01.azurewebsites.net'],
  },
  plugins: {
    SplashScreen: {
      launchShowDuration: 2000,
      backgroundColor: '#1976d2',
      showSpinner: false,
    },
  },
};

export default config;
