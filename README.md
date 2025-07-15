# AudioMuse AI-Plugin - The Jellyfin AudioMuse AI plugin middleware for Developer

AudioMuse-AI-Plugin is a Jellyfin middleware plugin that lets developers interact directly with Jellyfin’s API endpoints—eliminating the need to call the AudioMuse-AI service itself.  

> **Note:** This is an early, backend-only release. Only configuration front-end is included, and some features remain incomplete.


## Versioning TAG
You will always have a specific tag version pluse the laster version. In future the devel tag could also be included.

Here some example:
* **v0.0.1-alpha** (developer preview)  
* **latest** (always points to the newest release)


## Prerequisites

- A running Jellyfin server  
- An existing [AudioMuse-AI](https://github.com/NeptuneHub/AudioMuse-AI) deployment (in its own container)


## Installation and Configuration

* **Create the plugin folder:** Inside your Jellyfin plugin directory, create an `AudioMuseAI` folder. For example, in the linuxserver.io container:
```bash
mkdir -p /data/plugins/AudioMuseAI
cd /data/plugins/AudioMuseAI
````

* **Download the plugin DLL:** 
```bash
#For Latest release:
wget -O Jellyfin.Plugin.AudioMuseAi.dll \
"https://github.com/NeptuneHub/audiomuse-ai-plugin/releases/download/latest/Jellyfin.Plugin.AudioMuseAi.dll"

#For a Specific version (eg. v0.0.1-alpha)
wget -O Jellyfin.Plugin.AudioMuseAi.dll \
"https://github.com/NeptuneHub/audiomuse-ai-plugin/releases/download/v0.0.1-alpha/Jellyfin.Plugin.AudioMuseAi.dll"
```
* **Restart Jellyfin:** Just restart Jellyfin to have it charging the new plugin automatically.
* **Configure Audio-Muse AI endpoint:** Remember to go on the Plugin Configuration Page to add the url of your AudioMuse AI deployment

## Usage

Once Jellyfin is back online, the AudioMuse-AI middleware will be loaded automatically. Your applications can now call Jellyfin’s API endpoints directly—no additional proxying through the AudioMuse-AI service is required.

## Build yourself

If you want download the repo, do some change and then re-build locally, here the step:

* Download the repo locally and do your change
```
git clone https://github.com/NeptuneHub/audiomuse-ai-plugin.git
```

* go in the root folder of the repo and run this command:
```
dotnet restore && dotnet publish -c Release -o ./publish
```

* The only file that you need is this one, you can ignore all the other:
```
Jellyfin.Plugin.AudioMuseAi.dll
```

**Requirements:** For compiling the actual version of the repo you need dotnet-sdk-8.0, new version could require something newer, on Ubuntu/Debian install in this way:
```
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

## API CALL EXAMPLE
Below some API call example that you can run from linux cli, just remember to put in **YOUR-JELLYFIN-URL:PORT** and **YOUR-JELLYFIN-API-TOKEN**. For integration in a front-end you probably will not need the token because you will use the login session of the user.

For a more complete documentation rembemer to see the [AudioAMuse-AI](https://github.com/NeptuneHub/AudioMuse-AI) repo and also remember that the AudioMuse-AI API have an apiddocs that you can use like **http://YOUR-AUDIOMUSE-URL:PORT/apidocs/**. 

The aims is to replicate them, if this dosen't happen please feel a detailed issue (maybe with an example of call directly to AudioMuse-AI API and the different call to the AudioMuse-AI-Plugin API for check).

**1. Search Tracks**

```bash
curl -G 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/search_tracks' \
  --data-urlencode 'artist=red' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -H 'Accept: application/json'
```

---

**2. Similar Tracks**

```bash
curl -G 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/similar_tracks' \
  --data-urlencode 'item_id=07a998a337ab3fd4576006ae301d1d94' \
  --data-urlencode 'n=10' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -H 'Accept: application/json'
```

---

**3. Create Playlist**

```bash
curl -X POST 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/create_playlist' \
  -H 'Content-Type: application/json' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -d '{
        "playlist_name":"Similar to 21st Century2",
        "track_ids":[
          "07a998a337ab3fd4576006ae301d1d94","8d6bb1079eb9d6d16e4a5eb65435300d",
          "fe6981aa033a80d4594a4148171beb2f","4fc055539c1cba3f143384609d10a3f6",
          "54300affe6f9839596a37a3735690f92","31a92ee2d6f43da314cd8012b357cd8d",
          "a335ef8354cf8e8d97fe0a91efdb55eb","f9580efb7958d2454c76d8c463876cd3",
          "95f0ed149054afe123f5a7eb041add6f","f2ecc48919b82e4bffe00dd2f5297501",
          "6d54b36b7a6421e361a593c283319ddf"
        ]
      }'
```

---

**4. Start Analysis**

```bash
curl -X POST 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/analysis' \
  -H 'Content-Type: application/json' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -d '{
        "track_id":"07a998a337ab3fd4576006ae301d1d94",
        "analysis_type":"full"
      }'
```

---

**5. Cancel Task**

```bash
curl -X POST 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/cancel/d08b439f-ce5c-4ec7-b925-0c9a8320ba4b' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"'
```

---

**6. Last Task**

```bash
curl 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/last_task' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -H 'Accept: application/json'
```

---

**7. Active Tasks**

```bash
curl 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/active_tasks' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -H 'Accept: application/json'
```

---

**8. Clustering**

```bash
curl -X POST 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/clustering' \
  -H 'Content-Type: application/json' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -d '{
        "clustering_method":"kmeans",
        "max_distance":0.5,
        "max_songs_per_cluster":0,
        "pca_components_min":0,
        "pca_components_max":8,
        "clustering_runs":5000,
        "min_songs_per_genre_for_stratification":100,
        "stratified_sampling_target_percentile":50,
        "score_weight_diversity":2,
        "score_weight_purity":1,
        "score_weight_silhouette":0,
        "score_weight_davies_bouldin":0,
        "score_weight_calinski_harabasz":0,
        "score_weight_other_feature_diversity":0,
        "score_weight_other_feature_purity":0,
        "dbscan_eps_min":0.1,
        "dbscan_eps_max":0.5,
        "dbscan_min_samples_min":5,
        "dbscan_min_samples_max":20,
        "num_clusters_min":40,
        "num_clusters_max":100,
        "gmm_n_components_min":40,
        "gmm_n_components_max":100,
        "spectral_n_clusters_min":40,
        "spectral_n_clusters_max":100,
        "ai_model_provider":"NONE",
        "ollama_server_url":"http://192.168.3.15:11434/api/generate",
        "ollama_model_name":"mistral:7b",
        "gemini_api_key":"YOUR-GEMINI-API-KEY-HERE",
        "gemini_model_name":"gemini-1.5-flash-latest",
        "enable_clustering_embeddings":false
      }'
```

## Contributing & Feedback

This is an alpha release for testing. Please open issues or pull requests on GitHub to report bugs, request features, or contribute improvements.
