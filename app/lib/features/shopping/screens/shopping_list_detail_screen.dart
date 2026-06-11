import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/api/api_client.dart';
import '../../../core/models/product.dart';
import '../../../core/models/shopping.dart';

class ShoppingListDetailScreen extends StatefulWidget {
  final String listId;
  const ShoppingListDetailScreen({super.key, required this.listId});

  @override
  State<ShoppingListDetailScreen> createState() => _ShoppingListDetailScreenState();
}

class _ShoppingListDetailScreenState extends State<ShoppingListDetailScreen> {
  final _api = ApiClient();
  ShoppingList? _list;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final json = await _api.get('/shopping-lists/${widget.listId}');
      setState(() => _list = ShoppingList.fromJson(json));
    } catch (_) {
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _toggle(String itemId) async {
    await _api.patch('/shopping-lists/${widget.listId}/items/$itemId/toggle');
    _load();
  }

  Future<void> _showAddItem() async {
    await showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (_) => _AddItemSheet(
        api: _api,
        listId: widget.listId,
        onAdded: _load,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(_list?.name ?? 'Lista'),
        actions: [
          if (_list != null)
            TextButton.icon(
              onPressed: () => context.push('/shopping-lists/${widget.listId}/optimize'),
              icon: const Icon(Icons.route, color: Colors.white),
              label: const Text('Optimizar', style: TextStyle(color: Colors.white)),
            ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _showAddItem,
        backgroundColor: const Color(0xFF1D9E75),
        icon: const Icon(Icons.add, color: Colors.white),
        label: const Text('Agregar', style: TextStyle(color: Colors.white)),
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _list == null
              ? const Center(child: Text('No encontrada'))
              : _buildList(),
    );
  }

  Widget _buildList() {
    final pending = _list!.items.where((i) => !i.isPurchased).toList();
    final done = _list!.items.where((i) => i.isPurchased).toList();

    if (_list!.items.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.shopping_basket_outlined, size: 64, color: Colors.grey),
            const SizedBox(height: 12),
            Text('Lista vacía', style: TextStyle(color: Colors.grey[600])),
            const SizedBox(height: 8),
            ElevatedButton.icon(
              onPressed: _showAddItem,
              icon: const Icon(Icons.add),
              label: const Text('Agregar producto'),
            ),
          ],
        ),
      );
    }

    return ListView(
      padding: const EdgeInsets.only(bottom: 80, top: 8),
      children: [
        if (pending.isNotEmpty) ...[
          const Padding(
            padding: EdgeInsets.fromLTRB(16, 8, 16, 4),
            child: Text('PENDIENTES',
                style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: Colors.grey, letterSpacing: 0.5)),
          ),
          ...pending.map((i) => _buildItem(i)),
        ],
        if (done.isNotEmpty) ...[
          const Padding(
            padding: EdgeInsets.fromLTRB(16, 16, 16, 4),
            child: Text('COMPLETADOS',
                style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: Colors.grey, letterSpacing: 0.5)),
          ),
          ...done.map((i) => _buildItem(i)),
        ],
      ],
    );
  }

  Widget _buildItem(ShoppingListItem item) {
    return ListTile(
      leading: GestureDetector(
        onTap: () => _toggle(item.id),
        child: Icon(
          item.isPurchased ? Icons.check_circle : Icons.radio_button_unchecked,
          color: item.isPurchased ? const Color(0xFF1D9E75) : Colors.grey,
        ),
      ),
      title: Text(
        item.productName,
        style: TextStyle(
          decoration: item.isPurchased ? TextDecoration.lineThrough : null,
          color: item.isPurchased ? Colors.grey : null,
        ),
      ),
      subtitle: Text('x${item.quantity.toStringAsFixed(item.quantity == item.quantity.roundToDouble() ? 0 : 1)} ${item.unit}'),
      onTap: () => context.push('/compare/${item.productId}'),
    );
  }
}

class _AddItemSheet extends StatefulWidget {
  final ApiClient api;
  final String listId;
  final VoidCallback onAdded;

  const _AddItemSheet({required this.api, required this.listId, required this.onAdded});

  @override
  State<_AddItemSheet> createState() => _AddItemSheetState();
}

class _AddItemSheetState extends State<_AddItemSheet> {
  final _searchCtrl = TextEditingController();
  List<ProductSearchResult> _results = [];
  bool _searching = false;
  String? _adding;

  Future<void> _search(String q) async {
    if (q.trim().length < 2) {
      setState(() => _results = []);
      return;
    }
    setState(() => _searching = true);
    try {
      final data = await widget.api.getList('/products/search', params: {'q': q.trim()});
      setState(() => _results = data.map((j) => ProductSearchResult.fromJson(j)).toList());
    } catch (_) {
    } finally {
      setState(() => _searching = false);
    }
  }

  Future<void> _add(ProductSearchResult product) async {
    setState(() => _adding = product.id);
    try {
      await widget.api.post('/shopping-lists/${widget.listId}/items', {
        'productId': product.id,
        'quantity': 1,
        'unit': 0,
      });
      widget.onAdded();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('${product.name} agregado'),
            backgroundColor: const Color(0xFF1D9E75),
            duration: const Duration(seconds: 2),
          ),
        );
      }
    } catch (_) {
    } finally {
      if (mounted) setState(() => _adding = null);
    }
  }

  @override
  void dispose() {
    _searchCtrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const SizedBox(height: 12),
          Container(width: 36, height: 4, decoration: BoxDecoration(color: Colors.grey[300], borderRadius: BorderRadius.circular(2))),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
            child: TextField(
              controller: _searchCtrl,
              autofocus: true,
              decoration: InputDecoration(
                hintText: 'Buscar producto...',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _searching ? const Padding(padding: EdgeInsets.all(12), child: SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))) : null,
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              ),
              onChanged: _search,
            ),
          ),
          ConstrainedBox(
            constraints: BoxConstraints(maxHeight: MediaQuery.of(context).size.height * 0.45),
            child: _results.isEmpty
                ? Padding(
                    padding: const EdgeInsets.all(32),
                    child: Text(
                      _searchCtrl.text.isEmpty ? 'Escribe para buscar' : 'Sin resultados',
                      style: TextStyle(color: Colors.grey[500]),
                    ),
                  )
                : ListView.separated(
                    shrinkWrap: true,
                    itemCount: _results.length,
                    separatorBuilder: (_, _) => const Divider(height: 1),
                    itemBuilder: (_, i) {
                      final p = _results[i];
                      final isAdding = _adding == p.id;
                      return ListTile(
                        leading: Container(
                          width: 40,
                          height: 40,
                          decoration: BoxDecoration(color: const Color(0xFFE1F5EE), borderRadius: BorderRadius.circular(8)),
                          child: const Icon(Icons.inventory_2_outlined, color: Color(0xFF1D9E75), size: 20),
                        ),
                        title: Text(p.name, style: const TextStyle(fontSize: 14)),
                        subtitle: p.lowestPrice != null
                            ? Text('desde ₡${p.lowestPrice!.toStringAsFixed(0)}', style: const TextStyle(color: Color(0xFF1D9E75), fontSize: 12))
                            : null,
                        trailing: isAdding
                            ? const SizedBox(width: 24, height: 24, child: CircularProgressIndicator(strokeWidth: 2))
                            : IconButton(
                                icon: const Icon(Icons.add_circle_outline, color: Color(0xFF1D9E75)),
                                onPressed: () => _add(p),
                              ),
                        onTap: () => _add(p),
                      );
                    },
                  ),
          ),
          const SizedBox(height: 16),
        ],
      ),
    );
  }
}
