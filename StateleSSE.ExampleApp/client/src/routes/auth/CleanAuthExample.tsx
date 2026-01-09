import {useState} from "react";
import {useNavigate} from "react-router";
import toast from "react-hot-toast";
import {useUser} from "../../contexts/UserContext.tsx";
import type {AuthRequestDto, UserReturnDto} from "../../generated-client.ts";
import {ApiClient} from "../../utils/ApiClient.ts";
import {decodeJwt} from "../../utils/jwtHelper.ts";

export default function CleanAuthExample() {
  const [authForm, setAuthForm] = useState<AuthRequestDto>({
    password: "",
    name: "",
  });
  const [isLoading, setIsLoading] = useState(false);
  const navigate = useNavigate();
  const { setUser } = useUser();

  const handleAuth = async (isRegister: boolean) => {
    if (!authForm.name.trim() || !authForm.password.trim()) {
      toast.error("Please fill in all fields");
      return;
    }

    try {
      setIsLoading(true);
      const result = isRegister
        ? await ApiClient.register(authForm)
        : await ApiClient.login(authForm);

      localStorage.setItem("jwt", result.token!);
const decoded = decodeJwt(result.token!) as UserReturnDto;
      setUser(decoded);

      toast.success(isRegister ? "Welcome to Kahoot!" : "Welcome back!");
      navigate("/games");
    } catch (error) {
      console.error("Auth failed:", error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleSubmit = (e: React.FormEvent, isRegister: boolean) => {
    e.preventDefault();
    handleAuth(isRegister);
  };

  return (
    <div className="min-h-screen bg-base-200 flex items-center justify-center p-4">
      <div className="max-w-md w-full">
        <div className="text-center mb-8">
          <h1 className="text-5xl font-bold mb-2">🎯 Kahoot Clone</h1>
          <p className="text-lg text-base-content/70">
            Create quizzes and play with friends
          </p>
        </div>

        <div className="card bg-base-100 shadow-2xl">
          <div className="card-body">
            <h2 className="card-title text-2xl justify-center mb-6">
              Sign In or Register
            </h2>

            <form onSubmit={(e) => handleSubmit(e, false)}>
              <div className="form-control">
                <label className="label">
                  <span className="label-text font-semibold">Username</span>
                </label>
                <input
                  type="text"
                  placeholder="Enter your username"
                  className="input input-bordered input-lg"
                  value={authForm.name}
                  onChange={(e) =>
                    setAuthForm({ ...authForm, name: e.target.value })
                  }
                  disabled={isLoading}
                  required
                />
              </div>

              <div className="form-control mt-4">
                <label className="label">
                  <span className="label-text font-semibold">Password</span>
                </label>
                <input
                  type="password"
                  placeholder="Enter your password"
                  className="input input-bordered input-lg"
                  value={authForm.password}
                  onChange={(e) =>
                    setAuthForm({ ...authForm, password: e.target.value })
                  }
                  disabled={isLoading}
                  required
                />
              </div>

              <div className="form-control mt-8 gap-3">
                <button
                  type="submit"
                  className="btn btn-primary btn-lg"
                  disabled={isLoading}
                >
                  {isLoading ? (
                    <>
                      <span className="loading loading-spinner"></span>
                      Loading...
                    </>
                  ) : (
                    "Login"
                  )}
                </button>

                <button
                  type="button"
                  className="btn btn-outline btn-lg"
                  onClick={(e) => handleSubmit(e, true)}
                  disabled={isLoading}
                >
                  {isLoading ? (
                    <>
                      <span className="loading loading-spinner"></span>
                      Loading...
                    </>
                  ) : (
                    "Register"
                  )}
                </button>
              </div>
            </form>

            <div className="divider mt-6">OR</div>

            <div className="text-center text-sm text-base-content/70">
              <p>Create an account to start hosting quiz games,</p>
              <p>or login to join existing games!</p>
            </div>
          </div>
        </div>

        <div className="text-center mt-6 text-sm text-base-content/60">
          <p>Built with ❤️ using ASP.NET Core, SignalR, React & DaisyUI</p>
        </div>
      </div>
    </div>
  );
}
