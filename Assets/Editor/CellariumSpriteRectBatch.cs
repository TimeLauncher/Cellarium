using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class CellariumSpriteRectBatch
{
    [MenuItem("Cellarium/스프라이트 박스 일괄 확장")]
    private static void ExpandSpriteRects()
    {
        var paths = new HashSet<string>();

        // Project 창에서 선택한 폴더 또는 이미지 수집
        foreach (var selected in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(selected);

            if (AssetDatabase.IsValidFolder(path))
            {
                string[] guids = AssetDatabase.FindAssets(
                    "t:Texture2D",
                    new[] { path }
                );

                foreach (string guid in guids)
                {
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                }
            }
            else if (selected is Texture2D)
            {
                paths.Add(path);
            }
        }

        if (paths.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Sprite Rect Batch",
                "Project 창에서 이미지 또는 폴더를 선택해주세요.",
                "OK"
            );
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Sprite Rect 일괄 수정",
            $"최대 {paths.Count}개 이미지의 Sprite Rect를 " +
            "원본 이미지 전체 크기로 변경합니다.\n\n" +
            "원본 PNG와 PPU는 변경하지 않습니다.\n" +
            "진행할까요?",
            "수정",
            "취소"
        );

        if (!confirmed)
            return;

        var factory = new SpriteDataProviderFactories();
        factory.Init();

        int changed = 0;
        int skipped = 0;
        int failed = 0;

        foreach (string path in paths)
        {
            try
            {
                var importer =
                    AssetImporter.GetAtPath(path) as TextureImporter;

                // Multiple Sprite 이미지만 수정
                if (importer == null ||
                    importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != SpriteImportMode.Multiple)
                {
                    skipped++;
                    continue;
                }

                var provider =
                    factory.GetSpriteEditorDataProviderFromObject(importer);

                if (provider == null)
                {
                    skipped++;
                    continue;
                }

                provider.InitSpriteEditorDataProvider();

                var spriteRects = provider.GetSpriteRects();

                // 한 이미지에 Sprite Rect가 하나인 파일만 처리
                // 여러 개로 분할된 실제 스프라이트 시트는 건드리지 않음
                if (spriteRects.Length != 1)
                {
                    skipped++;
                    continue;
                }

                // 원본 PNG의 실제 해상도 확인
                importer.GetSourceTextureWidthAndHeight(
                    out int width,
                    out int height
                );

                // 기존 이름과 spriteID는 유지
                var spriteRect = spriteRects[0];

                spriteRect.rect = new Rect(
                    0, 0, width, height
                );

                spriteRect.alignment = SpriteAlignment.Center;
                spriteRect.pivot = new Vector2(0.5f, 0.5f);

                spriteRects[0] = spriteRect;

                provider.SetSpriteRects(spriteRects);
                provider.Apply();

                importer.SaveAndReimport();

                changed++;
            }
            catch (Exception e)
            {
                failed++;
                Debug.LogError(
                    $"Sprite Rect 수정 실패: {path}\n{e}"
                );
            }
        }

        EditorUtility.DisplayDialog(
            "일괄 수정 완료",
            $"수정: {changed}개\n" +
            $"건너뜀: {skipped}개\n" +
            $"실패: {failed}개",
            "확인"
        );

        Debug.Log(
            $"[Cellarium Sprite Rect] " +
            $"수정 {changed}, 건너뜀 {skipped}, 실패 {failed}"
        );
    }
}