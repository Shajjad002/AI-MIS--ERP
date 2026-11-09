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
      <div class="login-mark" aria-hidden="true">
        <svg viewBox="0 0 24 24" fill="none">
          <rect x="5" y="10" width="14" height="11" rx="2.5" fill="currentColor" />
          <path d="M8 10V7a4 4 0 1 1 8 0v3" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
          <circle cx="12" cy="15" r="1.2" fill="white" />
        </svg>
      </div>
      <p class="eyebrow">AI MIS &amp; ERP COPILOT</p>
      <h1>Welcome</h1>
      <p class="lede">Login to continue your journey</p>
      <form @submit.prevent="submit">
        <div class="login-field">
          <label for="user-name">Username</label>
          <input id="user-name" v-model="userName" autocomplete="username" placeholder="Enter your username" required />
        </div>
        <div class="login-field">
          <label for="password">Password</label>
          <input id="password" v-model="password" autocomplete="current-password" placeholder="Enter your password" required type="password" />
        </div>
        <button :disabled="loading" type="submit">
          {{ loading ? 'Signing in…' : 'Sign in' }}
          <svg v-if="!loading" viewBox="0 0 20 20" fill="none" aria-hidden="true"><path d="M4 10h12m-5-5 5 5-5 5" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" /></svg>
        </button>
      </form>
      <p v-if="error" class="error" role="alert">{{ error }}</p>
      <p class="login-footnote"><span aria-hidden="true"></span>Your connection is secure and encrypted</p>
    </section>
  </main>
</template>
