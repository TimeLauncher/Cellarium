using UnityEngine;
using System.IO;
using UnityEngine.SceneManagement;
public class MapCapture : MonoBehaviour
{
    public Camera captureCamera;
    public RenderTexture renderTexture;

    [ContextMenu("Save Map PNG")]
    public void SavePNG()
    {
        RenderTexture currentRT = RenderTexture.active;

        RenderTexture.active = renderTexture;

        captureCamera.targetTexture = renderTexture;
        captureCamera.Render();

        Texture2D image = new Texture2D(
            renderTexture.width,
            renderTexture.height,
            TextureFormat.RGBA32,
            false
        );

        image.ReadPixels(
            new Rect(0, 0, renderTexture.width, renderTexture.height),
            0,
            0
        );

        image.Apply();

        byte[] bytes = image.EncodeToPNG();

        string folderPath = Path.Combine(Application.dataPath, "MapCaptures");

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        string sceneName = SceneManager.GetActiveScene().name;
        string filePath = Path.Combine(folderPath, sceneName + ".png");

        File.WriteAllBytes(filePath, bytes);

        RenderTexture.active = currentRT;
        captureCamera.targetTexture = renderTexture;

        DestroyImmediate(image);

        Debug.Log("¸Ê PNG ÀúÀå ¿Ï·á: " + filePath);
    }
}