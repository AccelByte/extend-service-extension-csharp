# Update Guide

Quick steps to update your application with the latest changes from `origin/main` and handle any conflicts:

1. **Fetch the latest changes** without switching branches:
   ```bash
   git fetch origin main
   ```

2. **Merge the fetched updates** into your current branch:
   ```bash
   git merge origin/main
   ```
   
3. **Resolve any merge conflicts** that arise during the merge, then commit the resolved files:
   ```bash
   git add .
   git commit -m "chore: resolved merge conflicts"
   ```

By following these steps, you can efficiently update your application to include the latest changes from the main branch while preserving your custom modifications.