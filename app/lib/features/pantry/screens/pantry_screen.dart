import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../../core/api/api_client.dart';
import '../../../core/models/pantry.dart';
import '../../../core/models/product.dart';

class PantryScreen extends StatefulWidget {
  const PantryScreen({super.key});

  @override
  State<PantryScreen> createState() => _PantryScreenState();
}

class _PantryScreenState extends State<PantryScreen> {
  final _api = ApiClient();
  List<PantryItem> _items = [];
  List<Map<String, dynamic>> _lists = [];
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final results = await Future.wait([
        _api.getList('/pantry'),
        _api.getList('/shopping-lists'),
      ]);
      setState(() {
        _items = (results[0]).map((j) => PantryItem.fromJson(j as Map<String, dynamic>)).toList();
        _lists = (results[1]).cast<Map<String, dynamic>>();
      });
    } catch (_) {
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _addAllLowToList() async {
    if (_lists.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Primero crea una lista de compras')),
      );
      return;
    }

    final selected = await _pickList();
    if (selected == null || !mounted) return;

    final result = await _api.post('/pantry/add-low-stock-to-list/${selected['id']}', {});
    if (!mounted) return;
    final count = result['added'] as int? ?? 0;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(count > 0 ? '$count productos agregados a "${selected['name']}"' : 'Ya están todos en la lista'),
      backgroundColor: const Color(0xFF1D9E75),
    ));
    _load();
  }

  Future<void> _addItemToList(PantryItem item) async {
    if (_lists.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Primero crea una lista de compras')),
      );
      return;
    }
    final selected = await _pickList();
    if (selected == null || !mounted) return;
    await _api.post('/shopping-lists/${selected['id']}/items', {
      'productId': item.productId,
      'quantity': (item.minThreshold - item.quantity).clamp(1, 99),
      'unit': 0,
    });
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text('${item.productName} agregado a "${selected['name']}"'),
      backgroundColor: const Color(0xFF1D9E75),
    ));
  }

  Future<Map<String, dynamic>?> _pickList() {
    return showModalBottomSheet<Map<String, dynamic>>(
      context: context,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(16))),
      builder: (_) => Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const SizedBox(height: 12),
          Container(width: 36, height: 4, decoration: BoxDecoration(color: Colors.grey[300], borderRadius: BorderRadius.circular(2))),
          const Padding(
            padding: EdgeInsets.all(16),
            child: Text('Agregar a lista', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600)),
          ),
          ..._lists.map((l) => ListTile(
                leading: const Icon(Icons.shopping_cart_outlined, color: Color(0xFF1D9E75)),
                title: Text(l['name'] as String),
                onTap: () => Navigator.pop(context, l),
              )),
          const SizedBox(height: 16),
        ],
      ),
    );
  }

  Future<void> _deleteItem(PantryItem item) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Eliminar de despensa'),
        content: Text('¿Eliminar ${item.productName}?'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancelar')),
          TextButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Eliminar', style: TextStyle(color: Colors.red))),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    await _api.delete('/pantry/${item.id}');
    _load();
  }

  Future<void> _showAddProduct() async {
    await showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(16))),
      builder: (_) => _AddPantryItemSheet(api: _api, onAdded: _load),
    );
  }

  @override
  Widget build(BuildContext context) {
    final low = _items.where((i) => i.isRunningLow).toList();
    final ok = _items.where((i) => !i.isRunningLow).toList();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Mi despensa'),
        actions: [
          if (low.isNotEmpty)
            TextButton.icon(
              onPressed: _addAllLowToList,
              icon: const Icon(Icons.shopping_cart_outlined, color: Colors.white, size: 18),
              label: const Text('Agregar todo', style: TextStyle(color: Colors.white, fontSize: 13)),
            ),
        ],
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: _showAddProduct,
        backgroundColor: const Color(0xFF1D9E75),
        child: const Icon(Icons.add, color: Colors.white),
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _items.isEmpty
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.inventory_2_outlined, size: 64, color: Colors.grey),
                      const SizedBox(height: 12),
                      Text('Despensa vacía', style: TextStyle(color: Colors.grey[600])),
                      const SizedBox(height: 8),
                      const Text('Agrega productos para llevar control\nde tu inventario en casa.',
                          style: TextStyle(fontSize: 13, color: Colors.grey), textAlign: TextAlign.center),
                      const SizedBox(height: 20),
                      ElevatedButton.icon(
                        onPressed: _showAddProduct,
                        icon: const Icon(Icons.add),
                        label: const Text('Agregar producto'),
                      ),
                    ],
                  ),
                )
              : ListView(
                  padding: const EdgeInsets.fromLTRB(16, 16, 16, 80),
                  children: [
                    if (low.isNotEmpty) ...[
                      Row(
                        children: [
                          const Icon(Icons.warning_amber_rounded, color: Color(0xFFBA7517), size: 18),
                          const SizedBox(width: 6),
                          Text('Quedándose sin stock (${low.length})',
                              style: const TextStyle(fontWeight: FontWeight.w600, color: Color(0xFFBA7517))),
                        ],
                      ),
                      const SizedBox(height: 8),
                      ...low.map((i) => _buildLowItem(i)),
                      const SizedBox(height: 16),
                    ],
                    if (ok.isNotEmpty) ...[
                      const Text('EN STOCK',
                          style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: Colors.grey, letterSpacing: 0.5)),
                      const SizedBox(height: 8),
                      ...ok.map((i) => _buildStockItem(i)),
                    ],
                  ],
                ),
    );
  }

  Widget _buildLowItem(PantryItem item) {
    return Dismissible(
      key: Key(item.id),
      direction: DismissDirection.endToStart,
      background: Container(
        alignment: Alignment.centerRight,
        padding: const EdgeInsets.only(right: 16),
        color: Colors.red,
        child: const Icon(Icons.delete_outline, color: Colors.white),
      ),
      onDismissed: (_) => _deleteItem(item),
      child: Container(
        margin: const EdgeInsets.only(bottom: 8),
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: const Color(0xFFFAEEDA),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Column(
          children: [
            Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(item.productName,
                          style: const TextStyle(fontWeight: FontWeight.w500, color: Color(0xFF412402))),
                      Text('${_fmtQty(item.quantity)} ${item.unit} · mínimo ${_fmtQty(item.minThreshold)} ${item.unit}',
                          style: const TextStyle(fontSize: 12, color: Color(0xFF633806))),
                    ],
                  ),
                ),
                TextButton(
                  style: TextButton.styleFrom(
                    backgroundColor: const Color(0xFFBA7517),
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                    minimumSize: Size.zero,
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  ),
                  onPressed: () => _addItemToList(item),
                  child: const Text('+ Lista', style: TextStyle(fontSize: 12)),
                ),
              ],
            ),
            const SizedBox(height: 8),
            ClipRRect(
              borderRadius: BorderRadius.circular(3),
              child: LinearProgressIndicator(
                value: item.stockPercent,
                minHeight: 6,
                backgroundColor: Colors.white38,
                color: const Color(0xFFEF9F27),
              ),
            ),
            Align(
              alignment: Alignment.centerLeft,
              child: Text('${(item.stockPercent * 100).toStringAsFixed(0)}% restante',
                  style: const TextStyle(fontSize: 11, color: Color(0xFF633806))),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildStockItem(PantryItem item) {
    return Dismissible(
      key: Key(item.id),
      direction: DismissDirection.endToStart,
      background: Container(
        alignment: Alignment.centerRight,
        padding: const EdgeInsets.only(right: 16),
        color: Colors.red,
        child: const Icon(Icons.delete_outline, color: Colors.white),
      ),
      onDismissed: (_) => _deleteItem(item),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Row(
          children: [
            Container(
              width: 36,
              height: 36,
              decoration: BoxDecoration(
                  color: const Color(0xFFE1F5EE), borderRadius: BorderRadius.circular(8)),
              child: const Icon(Icons.inventory_2_outlined, color: Color(0xFF1D9E75), size: 18),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(item.productName, style: const TextStyle(fontSize: 14)),
                  const SizedBox(height: 4),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(3),
                    child: LinearProgressIndicator(
                      value: item.stockPercent,
                      minHeight: 5,
                      backgroundColor: Colors.grey[200],
                      color: item.stockPercent > 0.5 ? const Color(0xFF1D9E75) : const Color(0xFF9FE1CB),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 12),
            Text('${_fmtQty(item.quantity)} ${item.unit}',
                style: const TextStyle(fontSize: 13, color: Colors.grey)),
          ],
        ),
      ),
    );
  }

  String _fmtQty(double q) =>
      q == q.truncateToDouble() ? q.toInt().toString() : q.toStringAsFixed(1);
}

class _AddPantryItemSheet extends StatefulWidget {
  final ApiClient api;
  final VoidCallback onAdded;
  const _AddPantryItemSheet({required this.api, required this.onAdded});

  @override
  State<_AddPantryItemSheet> createState() => _AddPantryItemSheetState();
}

class _AddPantryItemSheetState extends State<_AddPantryItemSheet> {
  final _searchCtrl = TextEditingController();
  final _qtyCtrl = TextEditingController(text: '1');
  final _minCtrl = TextEditingController(text: '1');
  List<ProductSearchResult> _results = [];
  ProductSearchResult? _selected;
  bool _searching = false;
  bool _saving = false;

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

  Future<void> _save() async {
    if (_selected == null) return;
    final qty = double.tryParse(_qtyCtrl.text) ?? 1;
    final min = double.tryParse(_minCtrl.text) ?? 1;
    setState(() => _saving = true);
    try {
      await widget.api.post('/pantry', {
        'productId': _selected!.id,
        'quantity': qty,
        'unit': 0,
        'minThreshold': min,
      });
      widget.onAdded();
      if (mounted) Navigator.pop(context);
    } catch (_) {
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  void dispose() {
    _searchCtrl.dispose();
    _qtyCtrl.dispose();
    _minCtrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SizedBox(height: 12),
          Center(child: Container(width: 36, height: 4, decoration: BoxDecoration(color: Colors.grey[300], borderRadius: BorderRadius.circular(2)))),
          const Padding(
            padding: EdgeInsets.fromLTRB(16, 16, 16, 8),
            child: Text('Agregar a despensa', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w600)),
          ),
          const Divider(height: 1),
          Padding(
            padding: const EdgeInsets.all(16),
            child: _selected == null ? _buildSearch() : _buildQuantityForm(),
          ),
        ],
      ),
    );
  }

  Widget _buildSearch() {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        TextField(
          controller: _searchCtrl,
          autofocus: true,
          decoration: InputDecoration(
            hintText: 'Buscar producto...',
            prefixIcon: const Icon(Icons.search),
            suffixIcon: _searching
                ? const Padding(padding: EdgeInsets.all(12), child: SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2)))
                : null,
            border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
            contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          ),
          onChanged: _search,
        ),
        ConstrainedBox(
          constraints: BoxConstraints(maxHeight: MediaQuery.of(context).size.height * 0.35),
          child: _results.isEmpty
              ? Padding(
                  padding: const EdgeInsets.all(24),
                  child: Center(child: Text(_searchCtrl.text.isEmpty ? 'Escribe para buscar' : 'Sin resultados', style: TextStyle(color: Colors.grey[500]))),
                )
              : ListView.separated(
                  shrinkWrap: true,
                  itemCount: _results.length,
                  separatorBuilder: (_, _) => const Divider(height: 1),
                  itemBuilder: (_, i) => ListTile(
                    title: Text(_results[i].name, style: const TextStyle(fontSize: 14)),
                    subtitle: _results[i].brand != null ? Text(_results[i].brand!, style: const TextStyle(fontSize: 12)) : null,
                    onTap: () => setState(() => _selected = _results[i]),
                  ),
                ),
        ),
        const SizedBox(height: 8),
      ],
    );
  }

  Widget _buildQuantityForm() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ListTile(
          contentPadding: EdgeInsets.zero,
          leading: const Icon(Icons.inventory_2_outlined, color: Color(0xFF1D9E75)),
          title: Text(_selected!.name, style: const TextStyle(fontWeight: FontWeight.w500)),
          trailing: TextButton(
            onPressed: () => setState(() { _selected = null; _results = []; _searchCtrl.clear(); }),
            child: const Text('Cambiar'),
          ),
        ),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(
              child: TextField(
                controller: _qtyCtrl,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.]'))],
                decoration: InputDecoration(
                  labelText: 'Cantidad actual',
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                  contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: TextField(
                controller: _minCtrl,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.]'))],
                decoration: InputDecoration(
                  labelText: 'Mínimo',
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                  contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        SizedBox(
          height: 48,
          child: ElevatedButton(
            onPressed: _saving ? null : _save,
            child: _saving
                ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2))
                : const Text('Agregar a despensa'),
          ),
        ),
        const SizedBox(height: 8),
      ],
    );
  }
}
