import { useRouter } from "vue-router";
import { useAuth } from "@/composables/use-auth";

/** The sign-out click, shared by the reader and admin shells. Must be called
 * from a component setup (it resolves the router). */
export function useSignOut(): () => Promise<void> {
  const router = useRouter();
  const { logout } = useAuth();

  return async (): Promise<void> => {
    try {
      await logout();
    } catch {
      // Unreachable server: the cookie outlives this click, but the user asked
      // to leave — drop to the login screen and let a re-login overwrite it.
    }
    await router.replace({ name: "login" });
  };
}
