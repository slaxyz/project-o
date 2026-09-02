# Gacha Harvesting Game - Gameplay Roadmap

Document de cadrage pour le prototype Unity. Ce document décrit uniquement le gameplay de base. Les personnages, les rangs de gacha, les statistiques détaillées et les attaques seront définis plus tard.

## Vision du prototype

Jeu mobile en format vertical, avec une vue 3D top-down stylisée dans l'esprit des références : environnement dense, personnage lisible au centre et palette colorée forte.

Le joueur contrôle un personnage dans une forêt sans fin et infranchissable. Il donne des coups d'outil pour abattre les arbres et tuer les loups, remplit son sac de bois et de viande, revient au camp les vendre contre des dollars, et dépense ces dollars en outils, en sac plus grand, en vitesse et en bâtiments qui produisent tout seuls.

## Contraintes de base

- Plateforme cible : mobile.
- Orientation : portrait, ratio de référence 9:16.
- Caméra : 3D top-down légèrement inclinée, suivie par le joueur.
- Contrôle principal : joystick virtuel à gauche.
- Action principale : coup d'outil automatique sur tout ce qui est dans l'arc devant le joueur.
- Session : courte boucle de récolte, retour au camp, amélioration.

## Boucle de gameplay MVP

`Camp -> Exploration -> Récolte et combat -> Sac plein -> Vente au camp -> Achats -> Plus loin dans la forêt`

## Roadmap dans l'ordre

### 1. Projet et scène de test

- [x] Créer le projet Unity 3D.
- [x] Configurer le jeu en orientation portrait.
- [x] Créer une scène de test avec un sol, une lumière et une caméra.
- [x] Définir une résolution de référence 1080 x 1920.
- [x] Ajouter une organisation de dossiers : Scenes, Scripts, Prefabs, Materials, Models, UI, Audio.

### 2. Direction visuelle et assets placeholder

- [x] Définir une palette de couleurs et une référence visuelle cohérente (`Assets/ART_DIRECTION.md`).
- [x] Créer un personnage placeholder avec une silhouette lisible vue du dessus.
- [x] Créer un sol, des arbres et des rochers placeholder.
- [x] Créer les prefabs des ressources récoltables.
- [x] Créer un prefab simple pour le camp et le bâtiment améliorable.
- [x] Ajouter des matériaux simples avec couleurs et ombres lisibles sur mobile.
- [x] Préparer la liste d'assets définitifs à produire (`Assets/ART_ASSET_LIST.md`).

### 3. Joueur et caméra

- [x] Ajouter un personnage placeholder visible et identifiable.
- [x] Implémenter le déplacement avec un joystick virtuel.
- [x] Ajouter une caméra top-down légèrement inclinée qui suit le joueur.
- [x] Limiter le déplacement aux zones jouables.
- [x] Ajouter une animation ou un feedback de déplacement.

### 4. Interface mobile de base

- [x] Créer le HUD portrait avec compteur de ressources et capacité d'inventaire.
- [x] Ajouter le joystick virtuel à gauche.
- [x] Toutes les dépenses regroupées dans la boutique, grisées si trop chères.
- [x] Afficher clairement l'état de l'inventaire plein.
- [x] Créer un indicateur visuel pour la zone de dépôt du camp.
- [x] Créer un panneau simple d'amélioration avec coût, niveau et bouton d'action.
- [x] Ajouter les feedbacks UI : ressource obtenue, dépôt effectué, amélioration réussie.
- [x] Vérifier la lisibilité des textes et boutons en 9:16.
- [x] Barre de capacité segmentée bois / viande au lieu du simple compteur.
- [x] Une seule pastille de monnaie, et une barre de sac en trois segments (bois, viande, cash).
- [x] File de trois toasts empilés : deux gains simultanés restent tous les deux lisibles.
- [x] Boussole vers le camp avec distance, masquée quand on est au camp.
- [x] Tout le HUD est reconstruit par `WorldSetup` : plus rien de fait à la main.

### 5. Ressources et récolte

- [x] Créer des ressources placeholder récoltables (arbres, loups).
- [x] Peupler la zone automatiquement autour du joueur.
- [x] Donner des points de vie aux ressources et les récolter à leur mort.
- [x] Ajouter les effets de feedback : impact, disparition, objets qui volent vers le joueur.
- [x] Ajouter un son court de récolte et un son de ressource récupérée.
- [ ] Remplacer progressivement les placeholders par les premiers assets stylisés.

### 6. Inventaire et retour au camp

- [x] Ajouter une capacité d'inventaire limitée, partagée entre bois et viande.
- [x] Empêcher la récolte quand l'inventaire est plein.
- [x] Créer une zone de dépôt dans le camp.
- [x] Vendre le sac dans le camp et créditer les dollars au joueur.
- [x] Ajouter un feedback clair lors de la vente.
- [x] Empiler visuellement le bois et la viande portés dans le dos du personnage.

### 7. Camp et amélioration minimale

- [x] Créer un camp simple entouré d'un enclos.
- [x] Ajouter un bâtiment améliorable.
- [x] Définir un coût en dollars.
- [x] Afficher le coût et l'état de l'amélioration.
- [x] Modifier visuellement le bâtiment après amélioration.
- [x] Ajouter un feedback visuel et sonore lors de la construction.

### 8. Enclos et portail

- [x] Fermer le camp avec un anneau de clôtures sans chevauchement.
- [x] Laisser une entrée unique au sud, alignée sur le point de départ du joueur.
- [x] Portail à deux battants qui s'ouvre à l'approche et se referme derrière le joueur.
- [x] Battants avec colliders : portail fermé = passage bloqué.
- [ ] Ajouter les sons d'ouverture et de fermeture (champs prêts, clips à produire).

### 9. Forêt, clairières et chemins

- [x] Génération en chunks autour du joueur, forêt sans fin.
- [x] Placement bruité et jitté : dense mais jamais en grille visible.
- [x] **Forêt infranchissable** : troncs à ~1,5 m, on passe par les chemins ou on abat.
- [x] Clairières déterministes réparties dans la forêt.
- [x] Petits chemins sinueux reliant les clairières entre elles et au camp.
- [x] Sentier qui part droit du portail, pour ne pas sortir face à un mur de troncs.
- [x] Densité qui s'atténue au bord des clairières et des chemins.
- [x] Arbres proches en GameObjects avec collider, arbres lointains en rendu instancié.

### 10. Outils de récolte

- [x] Le personnage donne des coups d'outil : armement, frappe, retour élastique.
- [x] Les dégâts touchent tout ce qui est dans l'arc devant le joueur.
- [x] L'outil est rengainé quand le joueur entre au camp.
- [x] Cinq outils achetables en dollars, chacun avec son profil :
  - Hache : de départ, 1 dégât, 1,6 coup/s, arc 100°
  - Hache dorée (150 $) : 2 dégâts, +35% de butin
  - Tronçonneuse (600 $) : 3 dégâts, 3,2 coups/s, arc étroit
  - Débroussailleur (1 800 $) : arc 210°, coupe plusieurs arbres d'un coup
  - Énorme scie d'usine (5 000 $) : 7 dégâts, arc 280°, +50% de butin
- [x] Pas de niveaux : acheter l'outil suivant EST l'amélioration.
- [x] Auto-test de progression : le rendement compte l'arc, pas seulement le DPS mono-cible.

### 10b. Économie : la trappe, le cash et la cabane

- [x] **Trappe dans le sol** : deux battants qui s'ouvrent quand on se poste devant,
      et le sac s'y vide objet par objet.
- [x] La valeur s accumule dans la trappe et ressort en **liasses de 10 $** qui
      atterrissent dans le dos, comme une ressource.
- [x] Le cash se porte, prend de la place, et doit être rapporté à la **cabane**.
- [x] La cabane est au centre du camp, sur un socle beige. Elle remplace le bloc
      rouge, et le bâtiment de gauche a disparu : une seule base.
- [x] Déposer le cash à la cabane le transforme en dollars dépensables.
- [x] Bois 1 $, viande 3 $. Le pitch du son monte pendant la série.
- [x] **Une seule monnaie, aucun niveau** : tout est de l'équipement.

### 10c. Boutique

- [x] Menu latéral avec liste **scrollable** et bouton de fermeture.
- [x] Une carte par équipement : nom, statistiques, prix, ou EQUIP/EQUIPPED.
- [x] Section outils puis section sacs.
- [x] Cinq sacs, capacité qui double : 100, 200, 400, 800, 1600.
- [x] Le sac de départ tient **100** places.
- [x] La boutique passe au dessus du canvas du joystick, sinon les taps passent dessous.
- [x] Auto-tests : chaque palier d'outil et de sac est strictement meilleur et plus cher.

### 10d. Base niveau 2 et idle

- [x] Amélioration de base payée en dollars : agrandit le parc et révèle une parcelle.
- [x] La parcelle vide est solide : on ne marche pas dessus avant de construire.
- [x] Sur la parcelle, une scierie qui produit du bois par seconde en passif.

### 10f. Chantiers de clôture

- [x] Menu Build latéral accessible dans le camp.
- [x] Cinq apparences : clôture, rondins verticaux, rondins renforcés, pierre et acier.
- [x] Le chantier réserve le bois : il n'est plus vendu par la trappe.
- [x] Case de livraison dédiée près de la clôture avec bulle icône + progression.
- [x] Entrées supplémentaires payées en cash : une au niveau 1, deux au niveau 2.

### 10e. Pile portée

- [x] **Rendu instancié** au lieu de GameObjects : le nombre de colonnes est réellement
      illimité, un sac de 1600 bûches coûte deux draw calls au lieu de 1600 renderers.
- [x] 25 objets de haut par colonne, puis une nouvelle colonne.
- [x] Un rondin visuel représente 10 bois ; viande et cash continuent après le bois sans chevauchement.
- [x] Déchargement complet plafonné à 3 secondes, quelle que soit la capacité du sac.
- [x] Trois piles : bois, viande, liasses de cash.
- [x] Plus la tour est haute, plus elle oscille à la marche.
- [x] Chaque objet apparaît en rebond à sa place.

### 11. Loups et viande

- [x] Loup placeholder en formes simples.
- [x] Spawn par meute de 3, uniquement dans les clairières.
- [x] Rôde dans sa clairière, charge le joueur à vue, alerte la meute.
- [x] Bond qui repousse le joueur.
- [x] Ne rentre jamais dans le camp.
- [x] Donne de l'XP et de la viande, nouvelle ressource empilable comme le bois.
- [x] Une clairière nettoyée reste calme un moment avant qu'une meute revienne.

### 12. Juice

- [x] `Juice` : une seule bibliothèque de courbes (EaseOutBack, wobble amorti, arcs)
      utilisée par tout ce qui bouge, pour un feeling homogène.
- [x] `ScalePop` : coup de scale rebondissant, posable sur n'importe quoi. Les boutons
      se déclenchent tout seuls au clic.
- [x] Impact : l'arbre s'écrase, s'élargit, penche à l'opposé du coup et revient.
- [x] Mort d'arbre : étirement d'anticipation, chute sur un axe aléatoire, écrasement.
- [x] Mort de loup : saut, bascule, dégonflage.
- [x] Ressources : arc en cloche, pop à la sortie, absorption à l'arrivée.
- [x] Pile portée : chaque bûche apparaît en rebond, la pile oscille à la marche.
- [x] Joueur : squash and stretch synchronisé sur le pas.
- [x] Portail, outil dégainé, panneaux d'UI, compteurs : tous en EaseOutBack.

### 13. Reste à faire

- [ ] Les moutons reviennent en amélioration de camp (l'XP ayant disparu, ils donneront des dollars passifs).
- [ ] Vie du joueur et vraie conséquence en cas de morsure (aujourd'hui : recul seulement).
- [ ] Une seule essence d'arbre ; la référence en mélange deux.
- [ ] Sons dédiés : coup d'outil, mort de loup, portail, caisse enregistreuse.
- [ ] `ROADMAP.html` est une version rendue de ce document et n'est plus à jour.

## Notes techniques

- `Tools/Project O/Rebuild World` reconstruit toute la scène de façon déterministe.
  Le marqueur `Library/project-o-world-setup.txt` empêche de la reconstruire à chaque
  rechargement ; `Tools/Project O/Clear Setup Marker` le supprime.
- `ForestLayout` est la source unique de vérité sur les clairières et les chemins :
  la forêt et le spawner de loups la consultent tous les deux, personne ne la possède.
- Aucun système ne crée d'objet en mode édition. C'est ce qui avait rempli la scène
  de centaines de tuiles de sol.
