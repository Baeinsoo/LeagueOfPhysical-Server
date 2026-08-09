#if UNITY_EDITOR
using System;
using System.Text;

namespace LOP
{
    /// <summary>에디터 전용. 액세스 토큰 페이로드에서 <c>sub</c>만 꺼낸다.
    /// <para><b>서명을 검증하지 않는다.</b> 검증은 로비 introspect의 몫이고, 에디터는 조회 키를 git에
    /// 커밋하지 않으려고 introspect를 건너뛴다. 그 경계 안에서 "누가 접속했나"만 알기 위한 것이며,
    /// 파일 전체가 <c>UNITY_EDITOR</c>로 묶여 플레이어 빌드에는 들어가지 않는다.</para></summary>
    public static class EditorAccessTokenClaims
    {
        public static bool TryReadSubject(string accessToken, out string subject)
        {
            subject = null;

            if (string.IsNullOrEmpty(accessToken))
            {
                return false;
            }

            string[] parts = accessToken.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            string payload = DecodeBase64Url(parts[1]);
            if (payload == null)
            {
                return false;
            }

            const string key = "\"sub\":\"";
            int start = payload.IndexOf(key, StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            start += key.Length;
            int end = payload.IndexOf('"', start);
            if (end < 0)
            {
                return false;
            }

            subject = payload.Substring(start, end - start);
            return string.IsNullOrEmpty(subject) == false;
        }

        private static string DecodeBase64Url(string value)
        {
            try
            {
                string padded = value.Replace('-', '+').Replace('_', '/');
                padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
                return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif
