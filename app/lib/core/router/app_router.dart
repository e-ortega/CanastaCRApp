import 'package:go_router/go_router.dart';
import '../../features/auth/providers/auth_provider.dart';
import '../../features/auth/screens/login_screen.dart';
import '../../features/auth/screens/register_screen.dart';
import '../../features/compare/screens/compare_screen.dart';
import '../../features/pantry/screens/pantry_screen.dart';
import '../../features/search/screens/home_screen.dart';
import '../../features/search/screens/search_screen.dart';
import '../../features/shopping/screens/optimize_screen.dart';
import '../../features/shopping/screens/shopping_list_detail_screen.dart';
import '../../features/shopping/screens/shopping_lists_screen.dart';
import '../../shared/widgets/main_scaffold.dart';

GoRouter buildRouter(AuthProvider auth) => GoRouter(
      refreshListenable: auth,
      redirect: (context, state) {
        final loggedIn = auth.isAuthenticated;
        final onAuth = state.matchedLocation == '/login' || state.matchedLocation == '/register';
        if (!loggedIn && !onAuth) return '/login';
        if (loggedIn && onAuth) return '/';
        return null;
      },
      routes: [
        GoRoute(path: '/login', builder: (_, _) => const LoginScreen()),
        GoRoute(path: '/register', builder: (_, _) => const RegisterScreen()),
        ShellRoute(
          builder: (context, state, child) => MainScaffold(child: child),
          routes: [
            GoRoute(path: '/', builder: (_, _) => const HomeScreen()),
            GoRoute(path: '/search', builder: (_, _) => const SearchScreen()),
            GoRoute(
              path: '/compare/:productId',
              builder: (_, state) => CompareScreen(productId: state.pathParameters['productId']!),
            ),
            GoRoute(path: '/shopping-lists', builder: (_, _) => const ShoppingListsScreen()),
            GoRoute(
              path: '/shopping-lists/:id',
              builder: (_, state) => ShoppingListDetailScreen(listId: state.pathParameters['id']!),
            ),
            GoRoute(
              path: '/shopping-lists/:id/optimize',
              builder: (_, state) => OptimizeScreen(listId: state.pathParameters['id']!),
            ),
            GoRoute(path: '/pantry', builder: (_, _) => const PantryScreen()),
          ],
        ),
      ],
    );
