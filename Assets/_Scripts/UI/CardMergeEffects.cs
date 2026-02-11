using UnityEngine;
using System.Collections;

/// Optional visual effects for card merging
/// Add this component to enhance the merge experience with particles and effects
public class CardMergeEffects : MonoBehaviour
{
    [Header("Particle Settings")]
    [SerializeField] private GameObject mergeParticlePrefab;
    [SerializeField] private int particleCount = 20;
    [SerializeField] private float particleSpeed = 3f;
    [SerializeField] private float particleLifetime = 1f;
    
    [Header("Flash Effect")]
    [SerializeField] private Color[] flashColors = new Color[] 
    { 
        new Color(1f, 0.8f, 0.2f), // Gold
        Color.white,
        new Color(0.2f, 0.8f, 1f)  // Blue
    };
    [SerializeField] private int flashCount = 3;
    [SerializeField] private float flashDuration = 0.1f;
    
    [Header("Screen Shake")]
    [SerializeField] private bool enableScreenShake = true;
    [SerializeField] private float shakeIntensity = 0.1f;
    [SerializeField] private float shakeDuration = 0.2f;

    /// Play merge effect at the target card position
    public void PlayMergeEffect(Vector3 position, CardData upgradedData = null)
    {
        StartCoroutine(MergeEffectSequence(position, upgradedData));
    }

    private IEnumerator MergeEffectSequence(Vector3 position, CardData upgradedData)
    {
        // Flash effect
        if (flashColors.Length > 0)
        {
            for (int i = 0; i < flashCount; i++)
            {
                // You could add a screen overlay here
                yield return new WaitForSeconds(flashDuration);
            }
        }

        // Particle burst
        if (mergeParticlePrefab != null)
        {
            SpawnMergeParticles(position);
        }
        else
        {
            // Fallback: Create simple particle effect
            CreateSimpleParticles(position, upgradedData);
        }

        // Screen shake
        if (enableScreenShake && Camera.main != null)
        {
            StartCoroutine(ScreenShake());
        }
    }

    /// Spawn particles from a prefab
    private void SpawnMergeParticles(Vector3 position)
    {
        GameObject particleObj = Instantiate(mergeParticlePrefab, position, Quaternion.identity);
        Destroy(particleObj, particleLifetime);
    }

    /// Create simple sprite-based particles
    private void CreateSimpleParticles(Vector3 position, CardData upgradedData)
    {
        for (int i = 0; i < particleCount; i++)
        {
            GameObject particle = new GameObject("MergeParticle");
            particle.transform.position = position;
            
            SpriteRenderer sr = particle.AddComponent<SpriteRenderer>();
            sr.sprite = CreateParticleSprite();
            sr.sortingOrder = 200;
            
            // Use upgrade tier color if available
            if (upgradedData != null)
            {
                sr.color = upgradedData.themeColor;
            }
            else
            {
                sr.color = flashColors.Length > 0 ? flashColors[0] : Color.yellow;
            }
            
            // Random direction
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            StartCoroutine(AnimateParticle(particle, randomDir));
        }
    }

    private IEnumerator AnimateParticle(GameObject particle, Vector2 direction)
    {
        SpriteRenderer sr = particle.GetComponent<SpriteRenderer>();
        Vector3 startPos = particle.transform.position;
        Color startColor = sr.color;
        
        float elapsed = 0f;
        
        while (elapsed < particleLifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / particleLifetime;
            
            // Move outward
            particle.transform.position = startPos + (Vector3)(direction * particleSpeed * t);
            
            // Fade out
            Color c = startColor;
            c.a = 1f - t;
            sr.color = c;
            
            // Scale down
            particle.transform.localScale = Vector3.one * (1f - t * 0.5f);
            
            yield return null;
        }
        
        Destroy(particle);
    }

    private Sprite CreateParticleSprite()
    {
        Texture2D texture = new Texture2D(8, 8);
        
        // Create a simple circle
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(4, 4));
                Color col = dist < 3f ? Color.white : Color.clear;
                texture.SetPixel(x, y, col);
            }
        }
        
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8);
    }

    private IEnumerator ScreenShake()
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;
        
        Vector3 originalPos = cam.transform.localPosition;
        float elapsed = 0f;
        
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            
            float intensity = shakeIntensity * (1f - elapsed / shakeDuration);
            
            float offsetX = Random.Range(-intensity, intensity);
            float offsetY = Random.Range(-intensity, intensity);
            
            cam.transform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0);
            
            yield return null;
        }
        
        cam.transform.localPosition = originalPos;
    }

    /// Play a visual trail from source to target (for the merging card)
    public void PlayMergeTrail(Vector3 startPos, Vector3 endPos, float duration)
    {
        StartCoroutine(CreateTrail(startPos, endPos, duration));
    }

    private IEnumerator CreateTrail(Vector3 startPos, Vector3 endPos, float duration)
    {
        int trailSegments = 10;
        float elapsed = 0f;
        
        GameObject[] trailPieces = new GameObject[trailSegments];
        
        // Create trail pieces
        for (int i = 0; i < trailSegments; i++)
        {
            GameObject piece = new GameObject($"TrailPiece_{i}");
            SpriteRenderer sr = piece.AddComponent<SpriteRenderer>();
            sr.sprite = CreateParticleSprite();
            sr.color = flashColors.Length > 0 ? flashColors[0] : Color.yellow;
            sr.sortingOrder = 150 + i;
            
            trailPieces[i] = piece;
        }
        
        // Animate trail
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
            
            // Position each segment along the trail
            for (int i = 0; i < trailSegments; i++)
            {
                float segmentT = Mathf.Clamp01(t - (i * 0.05f));
                Vector3 segmentPos = Vector3.Lerp(startPos, endPos, segmentT);
                
                trailPieces[i].transform.position = segmentPos;
                
                // Fade out older segments
                SpriteRenderer sr = trailPieces[i].GetComponent<SpriteRenderer>();
                Color c = sr.color;
                c.a = 1f - (i / (float)trailSegments);
                sr.color = c;
            }
            
            yield return null;
        }
        
        // Clean up
        foreach (GameObject piece in trailPieces)
        {
            Destroy(piece);
        }
    }
}