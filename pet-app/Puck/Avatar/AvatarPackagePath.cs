using System.IO;

namespace Puck.Avatar;

/// 매니페스트가 부르는 이름을 그 아바타 자신의 폴더 기준으로 풀고,
/// 폴더 밖에 떨어지는 것은 전부 거절한다.
///
/// 검사 대상은 파일이 아니라 경로다: 패키지 안에 놓인 심볼릭 링크는
/// 여전히 링크가 가리키는 곳으로 간다. 그건 사람이 설치한 패키지이고
/// 이미지 자체와 같은 신뢰 수준이다. 여기서 막는 건 매니페스트가
/// 스스로 밖으로 손을 뻗는 것뿐이다.
public static class AvatarPackagePath
{
    public static string? ResolveFile(string directory, string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return null;
        // 드라이브 접두사(C:\)와 대체 데이터 스트림(file.png:hidden) 둘 다
        // 콜론으로 나타난다. 아바타 패키지 안의 정상적인 이름에는 콜론이
        // 쓰일 일이 없으므로 구분하지 않고 통째로 거절한다.
        if (relativePath.Contains(':')) return null;
        if (Path.IsPathRooted(relativePath)) return null;
        if (relativePath.StartsWith(@"\\") || relativePath.StartsWith("//")) return null;

        string root;
        string candidate;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory))
                   + Path.DirectorySeparatorChar;
            candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        }
        catch (Exception)
        {
            // 경로에 못 쓰는 문자가 섞였거나 너무 길다 — 읽을 수 없는 이름이다.
            return null;
        }

        // 구분자를 붙여서 비교한다: 없으면 "my-pet-evil"이 "my-pet"의
        // 안쪽으로 통과한다. Windows 파일 경로는 대소문자를 구분하지 않는다.
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }
}
