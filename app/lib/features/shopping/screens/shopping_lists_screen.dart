import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/api/api_client.dart';
import '../../../core/models/shopping.dart';

class ShoppingListsScreen extends StatefulWidget {
  const ShoppingListsScreen({super.key});

  @override
  State<ShoppingListsScreen> createState() => _ShoppingListsScreenState();
}

class _ShoppingListsScreenState extends State<ShoppingListsScreen> {
  final _api = ApiClient();
  List<ShoppingList> _lists = [];
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final data = await _api.getList('/shopping-lists');
      setState(() => _lists = data.map((j) => ShoppingList.fromJson(j)).toList());
    } catch (_) {
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _createList() async {
    final nameCtrl = TextEditingController();
    final name = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Nueva lista'),
        content: TextField(
          controller: nameCtrl,
          decoration: const InputDecoration(hintText: 'Nombre de la lista'),
          autofocus: true,
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancelar')),
          ElevatedButton(
              onPressed: () => Navigator.pop(ctx, nameCtrl.text.trim()),
              child: const Text('Crear')),
        ],
      ),
    );
    if (name != null && name.isNotEmpty) {
      await _api.post('/shopping-lists', {'name': name});
      _load();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Mis listas')),
      floatingActionButton: FloatingActionButton(
        onPressed: _createList,
        backgroundColor: const Color(0xFF1D9E75),
        child: const Icon(Icons.add, color: Colors.white),
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _lists.isEmpty
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.shopping_cart_outlined, size: 64, color: Colors.grey),
                      const SizedBox(height: 12),
                      Text('Sin listas todavía', style: TextStyle(color: Colors.grey[600])),
                      const SizedBox(height: 8),
                      ElevatedButton(onPressed: _createList, child: const Text('Crear lista')),
                    ],
                  ),
                )
              : ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: _lists.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 8),
                  itemBuilder: (_, i) {
                    final l = _lists[i];
                    return Dismissible(
                      key: Key(l.id),
                      direction: DismissDirection.endToStart,
                      background: Container(
                        alignment: Alignment.centerRight,
                        padding: const EdgeInsets.only(right: 20),
                        decoration: BoxDecoration(
                          color: Colors.red,
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: const Icon(Icons.delete_outline, color: Colors.white),
                      ),
                      confirmDismiss: (_) => showDialog<bool>(
                        context: context,
                        builder: (_) => AlertDialog(
                          title: const Text('Eliminar lista'),
                          content: Text('¿Eliminar "${l.name}" y todos sus artículos?'),
                          actions: [
                            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancelar')),
                            TextButton(
                              onPressed: () => Navigator.pop(context, true),
                              child: const Text('Eliminar', style: TextStyle(color: Colors.red)),
                            ),
                          ],
                        ),
                      ),
                      onDismissed: (_) async {
                        await _api.delete('/shopping-lists/${l.id}');
                        _load();
                      },
                      child: Card(
                        child: ListTile(
                          title: Text(l.name, style: const TextStyle(fontWeight: FontWeight.w500)),
                          subtitle: Text('${l.items.length} artículos · ${l.pendingCount} pendientes'),
                          trailing: const Icon(Icons.chevron_right),
                          onTap: () => context.push('/shopping-lists/${l.id}'),
                        ),
                      ),
                    );
                  },
                ),
    );
  }
}
