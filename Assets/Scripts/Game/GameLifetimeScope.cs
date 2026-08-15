using GameFramework.Runner;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 게임 씬의 게임 스코프. EnqueueParent(Room)로 로드되면 Room 자식으로 빌드된다.
    /// 게임마다 무엇이 달라지는지는 파생 스코프가 <see cref="ConfigureGame"/>에서 정한다.
    /// </summary>
    public abstract class GameLifetimeScope : LifetimeScope
    {
        [SerializeField, FormerlySerializedAs("gameEngine")] protected LOPRunner runner;

        protected override void Configure(IContainerBuilder builder)
        {
            new GameplayInstaller().Install(builder);

            // runner은 게임 서비스에 의존하므로 부모(Room)가 아닌 이 컨테이너에서 주입돼야 한다.
            builder.RegisterComponent(runner).As<IRunner>();
            // 룰이 sim 서비스로 쓰는 ITickUpdater (runner의 형제 컴포넌트). 호스트 역참조를 피하기 위해 직접 등록.
            builder.Register<ITickUpdater>(_ => runner.GetComponent<ITickUpdater>(), Lifetime.Singleton);

            ConfigureGame(builder);

            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);
                SceneManager.sceneLoaded += OnSceneLoaded;
            });
        }

        /// <summary>이 게임에서만 쓰는 등록 — 월드, 플레이어 몸 생성기, 룰.</summary>
        protected abstract void ConfigureGame(IContainerBuilder builder);

        protected override void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            base.OnDestroy();
        }

        // Factory가 additive 로드하는 맵 씬도 이 컨테이너로 주입한다.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 자기 씬은 빌드 콜백에서 이미 주입했다. (자기 씬 Awake 중 구독해 자기 sceneLoaded도 수신됨)
            if (scene == gameObject.scene)
            {
                Debug.Log($"[GameLifetimeScope] Skip re-injecting own scene '{scene.name}'; already injected in build callback.");
                return;
            }

            Container.InjectSceneObjects(scene);
        }
    }
}
