<template>
  <v-app>
    <AddServerSheet />
    <ClientProfileSheet />
    <ErrorSheet />
    <ServersSheet />
    <v-navigation-drawer
      app
      :width="250"
      temporary
      :disable-resize-watcher="false"
      :right="$vuetify.rtl"
      v-model="store.navigationDrawer"
    >
      <Navigation />
    </v-navigation-drawer>

    <v-main>
      <router-view />
    </v-main>
  </v-app>
</template>

<style>
html {
  overflow-y: auto !important;
}
</style>

<script>
import Navigation from "./components/Navigation";
import ErrorSheet from "./components/ErrorSheet";
import AddServerSheet from "./components/AddServerSheet";
import ClientProfileSheet from "./components/ClientProfileSheet";
import ServersSheet from "./components/ServersSheet";

export default {
  name: 'App',

  components: {
    Navigation,
    ErrorSheet,
    AddServerSheet,
    ClientProfileSheet,
    ServersSheet
  },

  data: () => ({
  })
};
</script>
