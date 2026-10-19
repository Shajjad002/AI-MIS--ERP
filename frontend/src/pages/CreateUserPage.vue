<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { apiFetch } from '../config/api'
import defaultProfileImageUrl from '../assets/default-profile.png'

const emit = defineEmits<{ back: [] }>()
const form = ref({ userName: '', displayName: '', email: '', password: '', role: 'MIS Analyst', branchCodes: '' })
const profileImage = ref<File | null>(null)
const imagePreviewUrl = ref('')
const profileImageInput = ref<HTMLInputElement | null>(null)
const message = ref('')
const error = ref('')
const loading = ref(false)
const profileImageLoading = ref(true)
const maxImageSizeBytes = 5 * 1024 * 1024

async function loadDefaultProfileImage() {
  profileImageLoading.value = true
  try {
    const response = await fetch(defaultProfileImageUrl)
    if (!response.ok) throw new Error(`Unable to load the default profile image (HTTP ${response.status}).`)
    const image = await response.blob()
    if (image.type !== 'image/png' || image.size > maxImageSizeBytes)
      throw new Error('The default profile image must be a PNG smaller than 5 MB.')

    profileImage.value = new File([image], 'default-profile.png', { type: image.type })
    imagePreviewUrl.value = URL.createObjectURL(image)
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Unable to load the default profile image.'
  } finally {
    profileImageLoading.value = false
  }
}

function clearImagePreview() {
  if (imagePreviewUrl.value) URL.revokeObjectURL(imagePreviewUrl.value)
  imagePreviewUrl.value = ''
}

function selectProfileImage(event: Event) {
  const input = event.target as HTMLInputElement
  const image = input.files?.[0]
  error.value = ''
  if (!image) return

  if (!['image/jpeg', 'image/png', 'image/webp'].includes(image.type)) {
    error.value = 'Choose a JPEG, PNG, or WebP image.'
    input.value = ''
    return
  }
  if (image.size > maxImageSizeBytes) {
    error.value = 'Profile image cannot exceed 5 MB.'
    input.value = ''
    return
  }

  clearImagePreview()
  profileImage.value = image
  imagePreviewUrl.value = URL.createObjectURL(image)
}

function removeProfileImage() {
  clearImagePreview()
  profileImage.value = null
  if (profileImageInput.value) profileImageInput.value.value = ''
}

async function create() {
  if (profileImageLoading.value) return
  error.value = ''
  message.value = ''
  loading.value = true

  try {
    const data = new FormData()
    data.append('userName', form.value.userName)
    data.append('displayName', form.value.displayName)
    data.append('email', form.value.email)
    data.append('password', form.value.password)
    data.append('role', form.value.role)
    data.append('branchCodes', form.value.branchCodes)
    if (profileImage.value) data.append('profileImage', profileImage.value)

    const response = await apiFetch('/api/users', {
      method: 'POST',
      headers: { Authorization: `Bearer ${localStorage.getItem('ai-mis.access-token') ?? ''}` },
      body: data,
    })
    const body = await response.json().catch(() => ({}))
    if (!response.ok) throw new Error(body.message ?? `User creation failed (HTTP ${response.status}).`)

    message.value = `User ${form.value.userName} was created successfully.`
    form.value = { userName: '', displayName: '', email: '', password: '', role: 'MIS Analyst', branchCodes: '' }
    clearImagePreview()
    profileImage.value = null
    await loadDefaultProfileImage()
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'User creation failed.'
  } finally {
    loading.value = false
  }
}

onMounted(loadDefaultProfileImage)
onBeforeUnmount(clearImagePreview)
</script>

<template>
  <main class="shell admin-shell create-user-page">
    <div class="page-heading">
      <div>
        <p class="eyebrow">Administration</p>
        <h1>Create user</h1>
        <p class="lede">Create an account and assign its initial access scope.</p>
      </div>
      <button class="secondary-button" type="button" @click="emit('back')">Back to users</button>
    </div>

    <form class="query-card user-form" @submit.prevent="create">
      <section class="profile-image-field" aria-labelledby="profile-image-title">
        <div class="profile-image-preview" aria-hidden="true">
          <img v-if="imagePreviewUrl" :src="imagePreviewUrl" alt="" />
          <svg v-else viewBox="0 0 24 24" fill="none">
            <circle cx="12" cy="8" r="3.5" />
            <path d="M4.5 20a7.5 7.5 0 0 1 15 0" />
          </svg>
        </div>
        <div class="profile-image-controls">
          <h2 id="profile-image-title">Profile image</h2>
          <p class="hint">{{ profileImageLoading ? 'Loading default profile image…' : 'JPEG, PNG, or WebP. Maximum 5 MB.' }}</p>
          <div class="profile-image-actions">
            <label class="profile-image-choose" for="profile-image">Choose image</label>
            <button v-if="profileImage" class="secondary-button" type="button" @click="removeProfileImage">Remove</button>
          </div>
          <input
            id="profile-image"
            ref="profileImageInput"
            class="profile-image-input"
            type="file"
            accept="image/jpeg,image/png,image/webp"
            @change="selectProfileImage"
          />
        </div>
      </section>

      <label>Username<input v-model="form.userName" required autocomplete="off" /></label>
      <label>Display name<input v-model="form.displayName" required /></label>
      <label>Email<input v-model="form.email" required type="email" /></label>
      <label>Temporary password<input v-model="form.password" required minlength="8" type="password" /></label>
      <label>
        Role
        <select v-model="form.role">
          <option>Administrator</option>
          <option>MIS Analyst</option>
          <option>Branch User</option>
        </select>
      </label>
      <label>Branch codes <span class="hint">(required for Branch User; comma separated)</span><input v-model="form.branchCodes" placeholder="0212, 0215" /></label>
      <button :disabled="loading || profileImageLoading" type="submit">{{ loading ? 'Creating user…' : 'Create user' }}</button>
      <p v-if="message" class="notice" role="status">{{ message }}</p>
      <p v-if="error" class="error" role="alert">{{ error }}</p>
    </form>
  </main>
</template>
