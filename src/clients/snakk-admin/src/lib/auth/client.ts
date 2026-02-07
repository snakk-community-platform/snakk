'use client'

import { create } from 'zustand';

interface User {
  userId: string;
  email: string;
  role: string;
}

interface AuthState {
  user: User | null;
  setUser: (user: User) => void;
  clearUser: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  setUser: (user) => set({ user }),
  clearUser: () => set({ user: null }),
}));

export async function login(email: string, password: string) {
  const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/admin/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
    credentials: 'include', // Important for cookies
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Login failed');
  }

  const data = await response.json();
  return data;
}

export async function logout() {
  await fetch(`${process.env.NEXT_PUBLIC_API_URL}/admin/auth/logout`, {
    method: 'POST',
    credentials: 'include',
  });

  useAuthStore.getState().clearUser();
}
