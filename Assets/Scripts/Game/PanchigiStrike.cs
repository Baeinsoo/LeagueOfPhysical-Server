using System;
using System.Numerics;

namespace LOP
{
    /// <summary>
    /// 판치기 타격의 힘 계산. 클라는 예측하지 않으므로 서버만 부른다.
    ///
    /// 원본(ForceElement)은 동전 밑 접촉점 수천 개마다 힘을 나눠 줬지만, 그 힘을 전부 *같은 지점*에
    /// 걸었기 때문에 합이 임펄스 하나와 수학적으로 같았다. 즉 격자가 만든 것은 회전이 아니라
    /// "동전이 판에 닿은 정도"라는 배수 하나였다. 여기서는 그 배수를 고정 개수 샘플로 직접 잰다.
    /// </summary>
    public static class PanchigiStrike
    {
        /// <summary>한 번의 타격이 무엇이었나 — 판 위 어디를, 어느 방향으로, 얼마나 오래 눌러서.</summary>
        public readonly struct StrikeInput
        {
            public readonly Vector3 StrikePoint;
            public readonly Vector3 DragDelta;
            public readonly float HoldTime;

            public StrikeInput(Vector3 strikePoint, Vector3 dragDelta, float holdTime)
            {
                StrikePoint = strikePoint;
                DragDelta = dragDelta;
                HoldTime = holdTime;
            }
        }

        /// <summary>타격 세기를 정하는 값들. 마스터데이터에서 온다.</summary>
        public readonly struct StrikeTuning
        {
            public readonly float ForceMultiplier;
            public readonly float HorizontalForceMultiplier;
            public readonly float FalloffRate;

            public StrikeTuning(float forceMultiplier, float horizontalForceMultiplier, float falloffRate)
            {
                ForceMultiplier = forceMultiplier;
                HorizontalForceMultiplier = horizontalForceMultiplier;
                FalloffRate = falloffRate;
            }
        }

        //  황금각(라디안). 해바라기 씨앗 배치가 원판을 고르게 덮는 데 쓰는 각도다.
        private const float GoldenAngle = 2.39996323f;

        /// <summary>
        /// 동전 하나에 줄 임펄스. 살아남은 샘플이 없으면 <see cref="Vector3.Zero"/>.
        /// </summary>
        /// <param name="liveSamples">판 밖·포개짐을 걸러 내고 남은 샘플들의 월드 좌표.</param>
        /// <param name="liveCount"><paramref name="liveSamples"/> 앞쪽 유효 개수.</param>
        /// <param name="totalSamples">걸러 내기 전 전체 샘플 수(K). 세기를 이 값으로 정규화한다.</param>
        public static Vector3 ComputeImpulse(in StrikeInput input, in StrikeTuning tuning,
            Vector3[] liveSamples, int liveCount, int totalSamples)
        {
            if (totalSamples <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalSamples),
                    "샘플 개수는 1 이상이어야 한다 — 마스터데이터를 확인할 것.");
            }
            if (liveCount <= 0)
            {
                return Vector3.Zero;
            }

            float sum = 0f;
            for (int i = 0; i < liveCount; i++)
            {
                //  감쇠는 판 위 평면 거리로만 잰다 — 동전이 떠 있어도 세기가 흔들리면 안 된다.
                float dx = liveSamples[i].X - input.StrikePoint.X;
                float dz = liveSamples[i].Z - input.StrikePoint.Z;
                sum += 1f / (1f + tuning.FalloffRate * (dx * dx + dz * dz));
            }

            //  K로 나누기 때문에 샘플을 늘려도 세기가 변하지 않는다 — 정밀도 노브와 세기 노브가 갈린다.
            float coverage = sum / totalSamples;

            return new Vector3(
                input.DragDelta.X * tuning.HorizontalForceMultiplier,
                input.HoldTime * tuning.ForceMultiplier,
                input.DragDelta.Z * tuning.HorizontalForceMultiplier) * coverage;
        }

        /// <summary>
        /// 동전 발자국(원) 위에 샘플을 고르게 깐다. 해바라기 배치라 개수가 몇이든 성립하고,
        /// 난수를 안 써서 같은 동전이면 항상 같은 자리가 나온다.
        /// </summary>
        public static void BuildSamples(Vector3 coinCenter, float radius, Vector3[] buffer)
        {
            if (buffer == null || buffer.Length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(buffer),
                    "샘플 버퍼는 1개 이상이어야 한다 — 마스터데이터를 확인할 것.");
            }

            int count = buffer.Length;
            for (int i = 0; i < count; i++)
            {
                //  sqrt를 씌워야 바깥쪽이 성기지 않다 — 원판은 반지름이 아니라 면적에 비례해 넓어진다.
                float r = radius * MathF.Sqrt((i + 0.5f) / count);
                float theta = i * GoldenAngle;
                buffer[i] = new Vector3(
                    coinCenter.X + r * MathF.Cos(theta),
                    coinCenter.Y,
                    coinCenter.Z + r * MathF.Sin(theta));
            }
        }
    }
}
