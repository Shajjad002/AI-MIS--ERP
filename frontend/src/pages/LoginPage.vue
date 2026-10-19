<script setup lang="ts">
import { ref } from 'vue'
import { useAuth } from '../composables/useAuth'

const userName = ref('')
const password = ref('')
const { loading, error, login } = useAuth()
const emit = defineEmits<{ authenticated: [] }>()

async function submit() {
  if (await login(userName.value, password.value)) emit('authenticated')
}
</script>

<template>
  <main class="auth-shell login-page">
    <section class="login-card">
      <p class="eyebrow">AI MIS & ERP Copilot</p>
      <h1>Welcome back.</h1>
      <p class="lede">Sign in to access secure ERP visualizations.</p>
      <form @submit.prevent="submit">
        <label for="user-name">Username</label>
        <input id="user-name" v-model="userName" autocomplete="username" required />
        <label for="password">Password</label>
        <input id="password" v-model="password" autocomplete="current-password" required type="password" />
        <button :disabled="loading" type="submit">{{ loading ? 'Signing in…' : 'Sign in' }}</button>
      </form>
      <p v-if="error" class="error" role="alert">{{ error }}</p>
    </section>
  </main>
</template>
