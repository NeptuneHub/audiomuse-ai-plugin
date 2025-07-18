![GitHub license](https://img.shields.io/github/license/neptunehub/audiomuse-ai-plugin.svg)
![Latest Tag](https://img.shields.io/github/v/tag/neptunehub/audiomuse-ai-plugin?label=latest-tag)
![Media Server Support: Jellyfin 10.10.7](https://img.shields.io/badge/Media%20Server-Jellyfin%2010.10.7-blue?style=flat-square&logo=server&logoColor=white)

# AudioMuse AI-Plugin - The Jellyfin AudioMuse AI plugin middleware for Developer

<p align="center">
  <img src="https://github.com/NeptuneHub/audiomuse-ai-plugin/blob/master/audiomuseai.png?raw=true" alt="AudioMuse-AI Logo" width="240">
</p>


AudioMuse-AI-Plugin is a Jellyfin middleware plugin that lets developers interact directly with Jellyfin’s API endpoints—eliminating the need to call the AudioMuse-AI service itself.

It can be also used by the final user if you want to gain advantages of the scheduled task, in fact it comes with:
* **Analysis task**: By default scheduled daily
* **Clustering task**: By default scheduled weekly

> **Note:** This is an alpha version plugin that work as a middleware. This means that you ALSO need to install AudioMuse-AI and this plugin interact with it.

# Table of Contents
* [Versioning TAG](#versioning-tag)
* [Prerequisites](#prerequisites)
* [Installation and Configuration](#installation-and-configuration)
* [Usage](#usage)
* [Build Yourself](#build-yourself)
* [API Call Example](#api-call-example)
  * [Search Tracks](#search-tracks)
  * [Similar Tracks](#similar-tracks)
  * [Create Playlist](#create-playlist)
  * [Start Analysis](#start-analysis)
  * [Cancel Task](#cancel-task)
  * [Last Task](#last-task)
  * [Active Tasks](#active-tasks)
  * [Clustering](#clustering)
  * [Instant Chat Playlist](#instant-chat-playlist)
* [Screenshots](#screenshots)
  * [Plugin Configurations Page](#plugin-configurations-page)
  * [Plugin Tasks Page](#plugin-tasks-page)
* [Contributing & Feedback](#contributing--feedback)



## Versioning TAG 
You will always have a specific tag version pluse the laster version. In future the devel tag could also be included.

Here some example:
* **v0.0.1-alpha** (developer preview)  
* **latest** (always points to the newest release)


## Prerequisites

- A running Jellyfin server  
- An existing [AudioMuse-AI](https://github.com/NeptuneHub/AudioMuse-AI) deployment (in its own container)


## Installation and Configuration
* Go on Jellyfin > Control Panel > Plugin Catalog
* Click on the gear-shaped settings icon on the top on the page to add a new manifest
* Add the AudioMuse AI manifest: https://raw.githubusercontent.com/NeptuneHub/audiomuse-ai-plugin/master/manifest.json
* Going back on Plugin Catalog youl will now show the plugin under the General section. Click on it and then install.
* **RESTART JELLYFIN**
* Now go back to the list of plugin installed, and you just need to configure the URL to reach AudioMuse-AI container, for example: http://192.168.3.14:8000

## Usage

**For Developer:** Once Jellyfin is back online, the AudioMuse-AI middleware will be loaded automatically. Your applications can now call Jellyfin’s API endpoints directly so no additional proxying through the AudioMuse-AI service is required.

**For the final user:** In the scheduled task section you will fine all the AudioMuse AI task. You can wait for their schedule or lunch directly (the first time is better to directly lunch them).

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
In this **build-yourself** scenario you will need to copy&past the dll in an AudioMuse-AI directory under plugin manully.

**Requirements:** For compiling the actual version of the repo you need dotnet-sdk-8.0, new version could require something newer, on Ubuntu/Debian install in this way:
```
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

## API CALL EXAMPLE
Below some API call example that you can run from linux cli, just remember to put in **YOUR-JELLYFIN-URL:PORT** and **YOUR-JELLYFIN-API-TOKEN**. For integration in a front-end you probably will not need the token because you will use the login session of the user.

For a more complete documentation rembemer to see the [AudioAMuse-AI](https://github.com/NeptuneHub/AudioMuse-AI) repo and also remember that the AudioMuse-AI API have an apiddocs that you can use like **http://YOUR-AUDIOMUSE-URL:PORT/apidocs/**. 

The aims is to replicate them 1:1, if this dosen't happen please feel a detailed issue (maybe with an example of call directly to AudioMuse-AI API and the different call to the AudioMuse-AI-Plugin API for check).

### Search Tracks

Used for the **similar track feature** to search for tracks by artist and retrieve matching items.

```bash
curl -G 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/search_tracks' \
  --data-urlencode 'artist=red' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -H 'Accept: application/json'
```

#### Output

```json
[
  {"author":"author1","item_id":"7190693ae7d0b7740fbfc26e5bddd0b3","title":"song1"},
  {"author":"author2","item_id":"e614f2119e654493012ea80f7dd5c617","title":"song2"},
  {"author":"author3","item_id":"6110790c7650a09bbc72a9db84987c50","title":"song3"},
  {"author":"author4","item_id":"f3365202f0bec5011d84ab2d07a10d5b","title":"song4"},
  {"author":"author5","item_id":"9387b1a062ede11cd92229c4d6c9c5e5","title":"song5"},
  … 
]
```

---

### Similar Tracks

Used for the **similar track feature**; you supply an `item_id` and it returns a list of similar tracks.

```bash
curl -G 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/similar_tracks' \
  --data-urlencode 'item_id=07a998a337ab3fd4576006ae301d1d94' \
  --data-urlencode 'n=10' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -H 'Accept: application/json'
```

#### Output

```json
[
  {"author":"author1","distance":0.356,"item_id":"8d6bb1079eb9d6d16e4a5eb65435300d","title":"song1"},
  {"author":"author2","distance":0.396,"item_id":"fe6981aa033a80d4594a4148171beb2f","title":"song2"},
  {"author":"author3","distance":0.431,"item_id":"4fc055539c1cba3f143384609d10a3f6","title":"song3"},
  {"author":"author4","distance":0.431,"item_id":"54300affe6f9839596a37a3735690f92","title":"song4"},
  {"author":"author5","distance":0.477,"item_id":"31a92ee2d6f43da314cd8012b357cd8d","title":"song5"},
  … 
]
```

---

### Create Playlist

Used for the **similar track feature** to create a playlist from a list of track IDs.

```bash
curl -X POST 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/create_playlist' \
  -H 'Content-Type: application/json' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -d '{
        "playlist_name":"Similar to 21st Century2",
        "track_ids":[
          "07a998a337ab3fd4576006ae301d1d94",
          "8d6bb1079eb9d6d16e4a5eb65435300d",
          "fe6981aa033a80d4594a4148171beb2f",
          "4fc055539c1cba3f143384609d10a3f6",
          "54300affe6f9839596a37a3735690f92",
          "31a92ee2d6f43da314cd8012b357cd8d",
          "a335ef8354cf8e8d97fe0a91efdb55eb",
          "f9580efb7958d2454c76d8c463876cd3",
          "95f0ed149054afe123f5a7eb041add6f",
          "f2ecc48919b82e4bffe00dd2f5297501",
          "6d54b36b7a6421e361a593c283319ddf"
        ]
      }'
```

#### Output

```json
{"message":"Playlist 'Similar to 21st Century2' created successfully!","playlist_id":null}
```

---

### Start Analysis

Start the analysis **batch task** using default AudioMuse-AI settings.

```bash
curl -X POST 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/analysis' \
  -H 'Content-Type: application/json' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -d '{}'
```

#### Output

```json
{"status":"queued","task_id":"218f4340-f784-45ea-9f84-a034b2ca2898","task_type":"main_analysis"}
```

---

### Cancel Task

Used to cancel a **batch task** (analysis or clustering).

```bash
curl -X POST 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/cancel/e14b1eb8-2641-4b1c-8853-6b16d726ff40' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"'
```

#### Output

```json
{"cancelled_jobs_count":5,"message":"Task e14b1eb8-2641-4b1c-8853-6b16d726ff40 and its children cancellation initiated. 5 total jobs affected.","task_id":"e14b1eb8-2641-4b1c-8853-6b16d726ff40"}
```

---

### Last Task

Used to retrieve the status of the last **batch task** run (analysis or clustering).

```bash
curl 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/last_task' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -H 'Accept: application/json'
```

#### Output

```json
{"details":{"message":"Task cancellation processed by API."},"progress":100,"running_time_seconds":38.143,"status":"REVOKED","task_id":"8a6b7eca-e85c-4065-8358-24f815f838a0","task_type":"album_analysis"}
```

---

### Active Tasks

Used to list any currently running **batch tasks** (analysis or clustering).

```bash
curl 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/active_tasks' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -H 'Accept: application/json'
```

#### Output

```json
{}
```

---

### Clustering

Start the clustering **batch task** using default AudioMuse-AI settings.

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

#### Output

```json
{"status":"queued","task_id":"1d609fa0-fc66-49e0-90b3-a5655b6c4292","task_type":"main_clustering"}
```

---

### Instant Chat Playlist

Used in the **Instant Playlist** feature to chat with the AI. You send a user query, and it returns both the AI’s reply and the list of tracks found.

```bash
curl -X POST 'http://YOUR-JELLYFIN-URL:PORT/AudioMuseAI/chat/playlist' \
  -H 'Content-Type: application/json' \
  -H 'Authorization: MediaBrowser Client="MyCLI", Device="Ubuntu CLI", DeviceId="ubuntu-cli-01", Version="1.0.0", Token="YOUR-JELLYFIN-API-TOKEN"' \
  -d '{
        "userInput":"Song with high energy",
        "ai_provider":"GEMINI",
        "ai_model":"gemini-1.5-flash-latest",
        "gemini_api_key":"YOUR-GEMINI-API-KEY"
      }'
```

#### Output

*Sample response not provided.*
## Screenshots

Here are a few glimpses of AudioMuse AI Plugin in action

### Plugin Configurations page

![Screenshot of AudioMuse AI Plugin's web interface showing the configuration page.](screen/config.png "Configuration Page")

### Plugin Tasks page

![Screenshot of AudioMuse AI Plugin's web interface showing the tasks page.](screen/task.png "Tasks Page")


## Contributing & Feedback

This is an alpha release for testing. Please open issues or pull requests on GitHub to report bugs, request features, or contribute improvements.
