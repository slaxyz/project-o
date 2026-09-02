# Gacha Harvesting Game - Gameplay Roadmap

Document de cadrage pour le prototype Unity. Ce document décrit uniquement le gameplay de base. Les personnages, les rangs de gacha, les statistiques détaillées et les attaques seront définis plus tard.

## Vision du prototype

Jeu mobile en format vertical, avec une vue 3D top-down stylisée dans l'esprit des références : environnement dense, personnage lisible au centre et palette colorée forte.

Le joueur contrôle un personnage dans une zone sauvage. Il récolte automatiquement les ressources proches, remplit son inventaire, revient au camp et utilise ses ressources pour améliorer la base et débloquer la zone suivante.

## Contraintes de base

- Plateforme cible : mobile.
- Orientation : portrait, ratio de référence 9:16.
- Caméra : 3D top-down légèrement inclinée, suivie par le joueur.
- Contrôle principal : joystick virtuel à gauche.
- Action principale : récolte automatique dans le rayon du personnage.
- Combat : hors périmètre du premier prototype ; les obstacles peuvent simplement bloquer la progression.
- Session : courte boucle de récolte, retour au camp, amélioration.

## Boucle de gameplay MVP

`Camp -> Exploration -> Récolte -> Inventaire plein -> Retour au camp -> Amélioration -> Nouvelle zone`

La récolte doit être immédiatement lisible et satisfaisante : ressource qui se détruit, petits effets visuels, objets qui se dirigent vers le personnage et compteur qui augmente.

## Roadmap dans l'ordre

### 1. Projet et scène de test

- [ ] Créer le projet Unity 3D.
- [ ] Configurer le jeu en orientation portrait.
- [ ] Créer une scène de test vide avec un sol, une lumière et une caméra.
- [ ] Définir une résolution de référence 1080 x 1920.
- [ ] Ajouter une organisation de dossiers : Scenes, Scripts, Prefabs, Materials, Models, UI, Audio.

### 2. Direction visuelle et assets placeholder

- [ ] Définir une palette de couleurs et une référence visuelle cohérente avec le style des images.
- [ ] Créer ou importer un personnage placeholder avec une silhouette lisible vue du dessus.
- [ ] Créer un sol, quelques arbres et quelques rochers placeholder.
- [ ] Créer les prefabs des ressources récoltables.
- [ ] Créer un prefab simple pour le camp et le bâtiment améliorable.
- [ ] Ajouter des matériaux simples avec couleurs et ombres lisibles sur mobile.
- [ ] Préparer une première liste d'assets définitifs à produire après validation du gameplay.

### 3. Joueur et caméra

- [ ] Ajouter un personnage placeholder visible et identifiable.
- [ ] Implémenter le déplacement avec un joystick virtuel.
- [ ] Ajouter une caméra top-down légèrement inclinée qui suit le joueur.
- [ ] Limiter le déplacement aux zones jouables.
- [ ] Ajouter une animation ou un feedback de déplacement.

### 4. Interface mobile de base

- [ ] Créer le HUD portrait avec compteur de ressources et capacité d'inventaire.
- [ ] Ajouter le joystick virtuel à gauche.
- [ ] Ajouter les boutons nécessaires côté droit, même s'ils sont inactifs au départ.
- [ ] Afficher clairement l'état de l'inventaire plein.
- [ ] Créer un indicateur visuel pour la zone de dépôt du camp.
- [ ] Créer un panneau simple d'amélioration avec coût, niveau et bouton d'action.
- [ ] Ajouter les feedbacks UI : ressource obtenue, dépôt effectué, amélioration réussie.
- [ ] Vérifier la lisibilité des textes et boutons en 9:16.

### 5. Ressources et récolte

- [ ] Créer une ressource placeholder récoltable.
- [ ] Placer plusieurs ressources dans la zone.
- [ ] Détecter automatiquement les ressources dans le rayon du joueur.
- [ ] Détruire une ressource après un délai de récolte simple.
- [ ] Ajouter les effets de feedback : animation, particules ou mouvement vers le joueur.
- [ ] Ajouter un son court de récolte et un son de ressource récupérée.
- [ ] Remplacer progressivement les placeholders par les premiers assets stylisés.

### 6. Inventaire et retour au camp

- [ ] Ajouter une capacité d'inventaire limitée.
- [ ] Empêcher ou ralentir la récolte quand l'inventaire est plein.
- [ ] Créer une zone de dépôt dans le camp.
- [ ] Vider l'inventaire dans le camp et créditer les ressources au joueur.
- [ ] Ajouter un feedback clair lors du dépôt.

### 7. Camp et amélioration minimale

- [ ] Créer un camp simple à côté de la zone de récolte.
- [ ] Ajouter un bâtiment améliorable.
- [ ] Définir un coût fixe en ressources.
- [ ] Afficher le coût et l'état de l'amélioration.
- [ ] Modifier visuellement le bâtiment après amélioration.
- [ ] Ajouter un feedback visuel et sonore lors de la construction.

### 8. Progression de zone

- [ ] Ajouter une limite ou un obstacle qui bloque une nouvelle zone.
- [ ] Définir une condition de déblocage liée à l'amélioration du camp.
- [ ] Ouvrir la nouvelle zone après validation de la condition.
- [ ] Ajouter quelques ressources différentes ou plus abondantes dans cette zone.
- [ ] Ajouter un asset placeholder pour la barrière, le portail ou l'obstacle.
- [ ] Ajouter un feedback UI lors du déblocage de la zone.

### 9. Première passe de sensation mobile

- [ ] Vérifier que le joueur, les ressources et les compteurs restent lisibles en portrait.
- [ ] Ajuster la vitesse de déplacement et le rayon de récolte.
- [ ] Ajuster la densité des ressources pour éviter une carte vide ou illisible.
- [ ] Tester la boucle complète sur appareil mobile ou résolution simulée.
- [ ] Tester les zones tactiles, les marges d'écran et les différentes tailles de téléphone.
- [ ] Vérifier que les effets, sons et animations renforcent la récolte sans surcharger l'écran.
- [ ] Corriger les frictions avant d'ajouter de nouvelles mécaniques.

## Hors périmètre jusqu'à validation du MVP

- Système de gacha et probabilités.
- Liste des personnages et tiers.
- Compétences, armes et statistiques avancées.
- Combat complet et ennemis complexes.
- Plusieurs biomes.
- Monétisation.

## Critère de validation du MVP

Le prototype est validé si un joueur peut comprendre sans explication qu'il doit se déplacer, récolter, revenir au camp, améliorer le bâtiment et accéder à une nouvelle zone, tout en trouvant la récolte agréable sur un écran vertical.
