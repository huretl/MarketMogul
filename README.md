# MarketMogul

Plugin [Dalamud](https://github.com/goatcorp/Dalamud) pour Final Fantasy XIV qui recommande quoi **acheter sur les autres mondes de ton data center** pour le **revendre sur ton monde d'origine**.

Le plugin est **consultatif** : il calcule et conseille, c'est toi qui achètes et mets en vente. Aucune automatisation d'achat ou de vente.

## Fonctionnement

1. Le plugin détecte seul ton monde d'origine, ton monde actuel et ton data center.
2. Un scan interroge l'API [Universalis](https://universalis.app) pour tous les objets vendables, en deux passes :
   - l'**historique des ventes** de ton monde sur 14 jours, pour tous les objets (endpoint `history`, rapide) ;
   - les **annonces** de ton monde et du data center, uniquement pour les objets qui se revendent assez.

   Les objets sans aucune vente sur ton monde sont mis **en sommeil** et ignorés pendant 24 h (cache local). Les requêtes partent en parallèle, dans les limites de Universalis, avec nouvelles tentatives en cas d'erreur passagère. Mesuré sur Omega (Chaos), 16 845 objets : **~30 s** au premier scan, **~17 s** ensuite.
3. Pour chaque objet (NQ et HQ), le moteur calcule :
   - un **prix de revente réaliste** : médiane des ventes des 14 derniers jours, ou juste sous l'annonce la moins chère si la concurrence affiche moins cher ;
   - la **vitesse de vente** sur ton monde, et donc la quantité que le marché peut absorber dans l'horizon de ton style de trading ;
   - les **annonces à acheter**, de la moins chère à la plus chère, tant qu'elles restent rentables après taxes (une annonce s'achète en entier) ;
   - une **fiabilité** (0–100 %) : fraîcheur des données, nombre de ventes observées, stabilité du prix.
4. Les opportunités sont triées par **profit par jour × fiabilité**.
5. En visite sur un autre monde, la fenêtre passe en **mode achat** et ne montre que ce qu'il y a à acheter là, avec un rappel dans le chat à l'arrivée.

### Marchands PNJ

Les objets vendus en gils par des marchands PNJ (lus dans les données du jeu) sont évalués comme une source d'achat supplémentaire : stock illimité, prix fixe, pas de taxe. Pour chaque objet, le scan garde la meilleure option entre le PNJ et les autres mondes. Dans le plan, ces achats apparaissent dans un groupe **Marchand PNJ**, visible depuis n'importe quel monde. L'emplacement du PNJ n'est pas encore indiqué.

### Plan d'achat

L'onglet **Plan** répartit les opportunités du scan selon tes ressources réelles :

- **budget** : tes gils d'inventaire, moins une réserve réglable ;
- **emplacements de vente** : 20 par servant, moins les annonces en cours, lus dans le jeu après un passage à la sonnette (valeur réglable en attendant).

L'optimiseur privilégie le meilleur profit, pondéré par la fiabilité, pour chaque gil et chaque emplacement consommé. Il tient compte de la taille de pile : un équipement prend un emplacement par unité. Une opération trop grosse est réduite à ses annonces les moins chères plutôt qu'écartée. Le plan est présenté **par monde**, comme un itinéraire, et reste figé pendant que tu achètes (bouton *Recalculer* sinon).

### Suivi des achats

Chaque achat au marketboard est enregistré automatiquement (demande d'achat puis confirmation du jeu) dans un journal sauvegardé sur ton PC :

- dans l'onglet **Plan**, les annonces achetées sont cochées et le total restant par monde se met à jour ;
- l'onglet **Journal** liste tes achats avec le prix de revente estimé au moment de l'achat et le profit attendu.

### Conseil d'achat au marketboard

Quand tu ouvres un objet au marketboard, un panneau s'accroche à droite de la liste des annonces. Il utilise les annonces **reçues en direct par le jeu**, plus fraîches que n'importe quel scan, et les prix de revente de ton monde d'origine, récupérés à la demande (cache de 10 minutes), même sans scan préalable. Pour chaque qualité (NQ/HQ) :

- prix de revente attendu, vitesse de vente, **prix maximum d'achat** et quantité maximale ;
- chaque annonce avec son servant, son profit par unité et un conseil : **ACHETER**, *Hors quota* (rentable mais au-delà de ce que ton monde absorbe) ou *Trop cher* ;
- le total à acheter : unités, coût, profit attendu.

Le nom du servant permet de retrouver chaque annonce dans la liste du jeu, quel que soit le tri choisi.

Commande : `/mogul`.

## Structure

```
src/MarketMogul.Core/    Moteur de calcul, sans dépendance à Dalamud (réutilisable et testable)
  Universalis/           Client HTTP de l'API Universalis (paquets de 100 objets, limitation de débit)
  Analysis/              Statistiques de marché, carnet d'ordres, scoring, scanner
src/MarketMogul/         Plugin Dalamud : détection du monde, catalogue des objets, fenêtres ImGui
tests/                   Tests unitaires du moteur (xUnit)
```

## Développement

Prérequis : [.NET 10 SDK](https://dotnet.microsoft.com/download) et XIVLauncher avec Dalamud installé (les librairies sont lues dans `%AppData%\XIVLauncher\addon\Hooks\dev`, ou dans le dossier indiqué par la variable `DALAMUD_HOME`).

```bash
dotnet build
dotnet test
```

Pour charger le plugin en jeu :

1. `/xlsettings` → **Experimental** → **Dev Plugin Locations** : ajoute le chemin complet vers `src/MarketMogul/bin/x64/Debug/MarketMogul.dll`.
2. `/xlplugins` → **Dev Tools** → **Installed Dev Plugins** → active MarketMogul.

La CI GitHub compile, lance les tests et publie le plugin en artefact à chaque push sur `main`.

## Installation via Dalamud

1. `/xlsettings` → **Experimental** → **Custom Plugin Repositories** : ajoute
   `https://raw.githubusercontent.com/huretl/MarketMogul/main/repo.json`, coche **Enabled**, puis **Save and Close**.
2. `/xlplugins` → cherche **MarketMogul** → **Install**.

Les mises à jour arrivent ensuite comme pour n'importe quel plugin.

## Publier une version

```bash
git tag v0.2.0
git push origin v0.2.0
```

Le workflow [Release](.github/workflows/release.yml) compile avec la version du tag, lance les tests, crée la release GitHub avec `MarketMogul.zip` et met à jour `repo.json` sur `main`. Dalamud détecte une mise à jour quand la version augmente : chaque tag doit être supérieur au précédent.

## Hypothèses à vérifier en jeu

- **Frais à la revente** : 5 % par défaut, hypothèse prudente. Réglable dans les réglages.
- Les données Universalis sont envoyées par les joueurs : une annonce peut avoir disparu. La fiabilité reflète ce risque.

## Feuille de route

- [x] Conseil d'achat à côté du marketboard, à partir des annonces en direct.
- [ ] Colorer directement les lignes rentables dans la liste du jeu (à valider en jeu).
- [x] Cocher les achats faits (événement d'achat du marketboard) et mettre à jour le plan.
- [x] Optimiseur de portefeuille : répartir ton budget (gils lus en jeu) et tes emplacements de vente.
- [ ] Prix conseillé et alertes de sous-cotation chez les servants.
- [x] Journal des achats avec profit attendu.
- [ ] Journal P&L complet : ventes réelles des servants, capital immobilisé, objets qui ne se vendent pas.
- [ ] Facteur de concurrence : stock déjà en vente et fréquence des sous-cotations.
- [x] Stratégie marchand PNJ → marketboard.
- [ ] Indiquer le PNJ et son emplacement pour les achats chez un marchand.
- [x] Scan rapide : pré-filtre sur l'historique, cache des objets en sommeil, requêtes parallèles.
